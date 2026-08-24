using System.Text.Json;
using HAMBOX.Application.Communication;
using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Promotions.Models;
using HAMBOX.Modules.Commerce.Application.Referrals;
using HAMBOX.Modules.Commerce.Domain.Enums;
using HAMBOX.Modules.Commerce.Domain.Operations;
using HAMBOX.Modules.Commerce.Domain.Orders;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HAMBOX.Modules.Commerce.Application.Services;

public enum DotVerificationOutcome
{
    Succeeded,
    Failed,
    StillPending,
    NotFound,
}

public sealed record DotVerificationResult(DotVerificationOutcome Outcome, Guid? OrderId, string? Message);

/// <summary>
/// The single, authoritative place a DOT payment attempt is ever resolved from Pending to a
/// terminal state. Invoked from three independent triggers — the browser redirect callback, DOT's
/// server-to-server notification webhook, and the background reconciliation sweep — all of which
/// race safely against each other via the guarded <c>Pending -&gt; Verifying</c> claim below, and
/// all of which get the same authoritative answer because none of them trust anything except this
/// service's own call to DOT's Check Transaction Status API.
/// </summary>
public sealed class DotPaymentVerificationService(
    ICommerceDbContext commerceDbContext,
    ICatalogDbContext catalogDbContext,
    ICommerceTransactionService transactionService,
    IDotPaymentGateway dotGateway,
    OrderFulfillmentService fulfillmentService,
    PromotionRedemptionService promotionRedemptionService,
    ReferralLifecycleService referralLifecycle,
    ICommunicationService communicationService,
    IOperationalJobQueue jobQueue,
    ILogger<DotPaymentVerificationService> logger)
{
    private const decimal AmountToleranceUsd = 0.01m;

    public async Task<DotVerificationResult> VerifyAndFinalizeAsync(Guid paymentAttemptId, CancellationToken cancellationToken)
    {
        var attempt = await commerceDbContext.PaymentAttempts.FirstOrDefaultAsync(p => p.Id == paymentAttemptId, cancellationToken);
        if (attempt is null)
        {
            return new DotVerificationResult(DotVerificationOutcome.NotFound, null, null);
        }

        if (attempt.Status != PaymentAttemptStatus.Pending)
        {
            // Already terminal, or already being verified by a concurrent caller — either way,
            // safe to report back as the attempt's current state rather than treat as an error.
            // This is what makes duplicate callbacks/notification retries idempotent.
            return MapToResult(attempt);
        }

        attempt.BeginVerification();

        try
        {
            await commerceDbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Lost the race to claim this attempt to a concurrent caller (callback vs. webhook vs.
            // reconciliation sweep, all landing at the same instant) — re-read whatever they left it as.
            return await ReportCurrentStateAsync(paymentAttemptId, cancellationToken);
        }

        var order = await commerceDbContext.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == attempt.OrderId, cancellationToken);

        if (order is null)
        {
            logger.LogError("DOT payment attempt {PaymentAttemptId} references missing order {OrderId}.", attempt.Id, attempt.OrderId);
            attempt.MarkFailed("ORDER_NOT_FOUND", "The associated order could not be found.");
            await commerceDbContext.SaveChangesAsync(cancellationToken);
            return new DotVerificationResult(DotVerificationOutcome.Failed, attempt.OrderId, "Order not found.");
        }

        var statusResult = await dotGateway.CheckTransactionStatusByPartnerTxIdAsync(attempt.PartnerTxId, attempt.OperatorId, cancellationToken);

        if (statusResult.IsFailure)
        {
            // Transient provider/network failure — release the claim back to Pending so a later
            // callback, webhook retry, or the reconciliation sweep can try again. Never guess.
            attempt.ReleaseForRetry();
            await commerceDbContext.SaveChangesAsync(cancellationToken);

            logger.LogWarning(
                "DOT Check Transaction Status unavailable for payment attempt {PaymentAttemptId}: {Error}",
                paymentAttemptId, statusResult.Error.Description);

            return new DotVerificationResult(DotVerificationOutcome.StillPending, order.Id, "Payment provider temporarily unavailable.");
        }

        var status = statusResult.Value;

        if (!status.IsSuccessfulTransaction)
        {
            attempt.MarkFailed(status.ResultCode.ToString(), status.ResultDesc);
            order.MarkFailed();
            RecordAudit(order.Id, "VerificationFailed", "Failed", attempt, status.ResultDesc);
            await commerceDbContext.SaveChangesAsync(cancellationToken);
            return new DotVerificationResult(DotVerificationOutcome.Failed, order.Id, status.ResultDesc);
        }

        if (status.Amount is not decimal verifiedAmount
            || status.Currency is not string verifiedCurrency
            || Math.Abs(verifiedAmount - attempt.ExpectedAmount) > AmountToleranceUsd
            || !string.Equals(verifiedCurrency, attempt.ExpectedCurrency, StringComparison.OrdinalIgnoreCase))
        {
            // DOT itself says "successful transaction" but what it actually charged doesn't match
            // what HAMBOX priced this order at when it initiated the attempt. Never fulfill on a
            // mismatch, no matter how the transaction is labeled.
            logger.LogError(
                "DOT verified amount/currency mismatch for payment attempt {PaymentAttemptId}: expected {ExpectedAmount} {ExpectedCurrency}, DOT reported {VerifiedAmount} {VerifiedCurrency}.",
                paymentAttemptId, attempt.ExpectedAmount, attempt.ExpectedCurrency, status.Amount, status.Currency);

            attempt.MarkFailed("AMOUNT_MISMATCH", "Verified amount did not match the expected charge.");
            order.MarkFailed();
            RecordAudit(order.Id, "VerificationFailed", "AmountMismatch", attempt, "Verified amount/currency mismatch.");
            await commerceDbContext.SaveChangesAsync(cancellationToken);
            return new DotVerificationResult(DotVerificationOutcome.Failed, order.Id, "Payment verification failed.");
        }

        await transactionService.ExecuteAsync(async ct =>
        {
            var providerTransactionId = attempt.ProviderTransactionId ?? attempt.PartnerTxId;
            attempt.MarkSucceeded(providerTransactionId, verifiedAmount, verifiedCurrency);
            order.RecordPayment("Dot", providerTransactionId);
            RecordAudit(order.Id, "VerificationSucceeded", "Paid", attempt, status.ResultDesc);

            if (!string.IsNullOrWhiteSpace(attempt.PendingPromotionsJson))
            {
                var appliedPromotions = JsonSerializer.Deserialize<List<AppliedPromotionDto>>(attempt.PendingPromotionsJson) ?? [];
                if (appliedPromotions.Count > 0)
                {
                    await promotionRedemptionService.RedeemAsync(order, appliedPromotions, order.UserId, ct);
                }
            }

            // Awards the referrer's points if this order qualifies — same points-only path every
            // other completed order goes through.
            await referralLifecycle.ProcessOrderCompletedAsync(order, ct);

            // Only now — after authoritative, amount-validated confirmation — does inventory ever
            // get reserved and committed for this order. Digital codes were never touched while the
            // customer was off completing OTP with DOT.
            try
            {
                await fulfillmentService.FulfillMissingAsync(order, ct);
            }
            catch (InvalidOperationException ex)
            {
                // The captured payment must never be undone by a delivery-side problem (e.g. a
                // genuine stock race). Leave the order for the retry job enqueued below instead of
                // letting this escape and strand the payment attempt mid-transaction.
                logger.LogError(
                    ex, "DOT payment attempt {PaymentAttemptId} succeeded but fulfillment failed; will retry.", attempt.Id);
            }

            await commerceDbContext.SaveChangesAsync(ct);
            await catalogDbContext.SaveChangesAsync(ct);
        }, cancellationToken);

        if (order.Status != OrderStatus.Completed)
        {
            // Paid but not (yet, or fully) delivered — same safety net an admin-triggered
            // fulfillment retry uses, not a DOT-specific path.
            await jobQueue.EnqueueAsync(
                OperationalJobTypes.RetryOrderFulfillment,
                JsonSerializer.Serialize(new { orderId = order.Id }),
                OperationalJobPriority.High,
                relatedEntityType: "Order",
                relatedEntityId: order.Id.ToString(),
                cancellationToken: cancellationToken);
        }

        await communicationService.SendAsync(new CommunicationRequest(
            UserId: order.UserId,
            TemplateKey: "OrderConfirmation",
            Category: CommunicationCategory.Order,
            Variables: new Dictionary<string, string>
            {
                ["OrderNumber"] = order.OrderNumber,
                ["Total"] = order.TotalAmount.ToString("0.00"),
            },
            RelatedEntityType: "Order",
            RelatedEntityId: order.Id.ToString(),
            ActionUrl: $"/account/library?orderId={order.Id}"), cancellationToken);

        return new DotVerificationResult(DotVerificationOutcome.Succeeded, order.Id, null);
    }

    private async Task<DotVerificationResult> ReportCurrentStateAsync(Guid paymentAttemptId, CancellationToken cancellationToken)
    {
        var existing = await commerceDbContext.PaymentAttempts
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == paymentAttemptId, cancellationToken);

        return existing is null
            ? new DotVerificationResult(DotVerificationOutcome.NotFound, null, null)
            : MapToResult(existing);
    }

    private static DotVerificationResult MapToResult(PaymentAttempt attempt) => attempt.Status switch
    {
        PaymentAttemptStatus.Succeeded => new DotVerificationResult(DotVerificationOutcome.Succeeded, attempt.OrderId, null),
        PaymentAttemptStatus.Failed => new DotVerificationResult(DotVerificationOutcome.Failed, attempt.OrderId, attempt.LastReasonDescription),
        PaymentAttemptStatus.Expired => new DotVerificationResult(DotVerificationOutcome.Failed, attempt.OrderId, "The payment window expired."),
        _ => new DotVerificationResult(DotVerificationOutcome.StillPending, attempt.OrderId, null),
    };

    private void RecordAudit(Guid orderId, string eventType, string status, PaymentAttempt attempt, string? providerMessage)
    {
        var payload = JsonSerializer.Serialize(new
        {
            attempt.PartnerTxId,
            attempt.ProviderTransactionId,
            attempt.OperatorId,
            attempt.ServiceId,
            attempt.ExpectedAmount,
            attempt.ExpectedCurrency,
            attempt.VerifiedAmount,
            attempt.VerifiedCurrency,
            attempt.MaskedMsisdn,
            ProviderMessage = providerMessage,
        });

        commerceDbContext.OrderPaymentCallbacks.Add(OrderPaymentCallback.Create(
            orderId, "Dot", eventType, status, attempt.ProviderTransactionId, payload));
    }
}
