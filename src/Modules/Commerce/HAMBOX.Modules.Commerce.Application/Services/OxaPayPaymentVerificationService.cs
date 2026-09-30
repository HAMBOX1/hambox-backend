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

public enum OxaPayVerificationOutcome
{
    Succeeded,
    Failed,
    StillPending,
    NotFound,
}

public sealed record OxaPayVerificationResult(OxaPayVerificationOutcome Outcome, Guid? OrderId, string? Message);

/// <summary>
/// The single, authoritative place an OxaPay payment attempt is ever resolved from Pending to a
/// terminal state. Invoked from two independent triggers — OxaPay's webhook and the active-poll
/// re-verification on the result page — both of which race safely against each other via the
/// guarded <c>Pending -&gt; Verifying</c> claim below. Mirrors
/// <c>CryptomusPaymentVerificationService</c> exactly, except the authoritative status call is keyed
/// by OxaPay's own <c>track_id</c> (<see cref="PaymentAttempt.ProviderTransactionId"/>) rather than a
/// HAMBOX-supplied order id — OxaPay's Payment Information endpoint has no merchant-key lookup.
/// </summary>
public sealed class OxaPayPaymentVerificationService(
    ICommerceDbContext commerceDbContext,
    ICatalogDbContext catalogDbContext,
    ICommerceTransactionService transactionService,
    IOxaPayPaymentGateway oxapayGateway,
    OrderFulfillmentService fulfillmentService,
    PromotionRedemptionService promotionRedemptionService,
    ReferralLifecycleService referralLifecycle,
    ICommunicationService communicationService,
    IOperationalJobQueue jobQueue,
    ILogger<OxaPayPaymentVerificationService> logger)
{
    private const decimal AmountToleranceUsd = 0.01m;

    public async Task<OxaPayVerificationResult> VerifyAndFinalizeAsync(Guid paymentAttemptId, CancellationToken cancellationToken)
    {
        var attempt = await commerceDbContext.PaymentAttempts.FirstOrDefaultAsync(p => p.Id == paymentAttemptId, cancellationToken);
        if (attempt is null)
        {
            return new OxaPayVerificationResult(OxaPayVerificationOutcome.NotFound, null, null);
        }

        if (attempt.Status != PaymentAttemptStatus.Pending)
        {
            return MapToResult(attempt);
        }

        if (string.IsNullOrWhiteSpace(attempt.ProviderTransactionId))
        {
            // No OxaPay track_id was ever recorded — CreateInvoice must have failed before the
            // attempt got this far. Nothing to verify against; leave it for the reconciliation
            // sweep / natural expiry rather than guessing.
            logger.LogWarning("OxaPay payment attempt {PaymentAttemptId} has no track_id; cannot verify.", attempt.Id);
            return new OxaPayVerificationResult(OxaPayVerificationOutcome.StillPending, attempt.OrderId, null);
        }

        attempt.BeginVerification();

        try
        {
            await commerceDbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Lost the race to claim this attempt to a concurrent caller (webhook vs. active-poll
            // re-verify, landing at the same instant) — re-read whatever they left it as.
            return await ReportCurrentStateAsync(paymentAttemptId, cancellationToken);
        }

        var order = await commerceDbContext.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == attempt.OrderId, cancellationToken);

        if (order is null)
        {
            logger.LogError("OxaPay payment attempt {PaymentAttemptId} references missing order {OrderId}.", attempt.Id, attempt.OrderId);
            attempt.MarkFailed("ORDER_NOT_FOUND", "The associated order could not be found.");
            await commerceDbContext.SaveChangesAsync(cancellationToken);
            return new OxaPayVerificationResult(OxaPayVerificationOutcome.Failed, attempt.OrderId, "Order not found.");
        }

        var statusResult = await oxapayGateway.GetPaymentInfoByTrackIdAsync(attempt.ProviderTransactionId, cancellationToken);

        if (statusResult.IsFailure)
        {
            // Transient provider/network failure — release the claim back to Pending so a later
            // webhook or the reconciliation sweep can try again. Never guess.
            attempt.ReleaseForRetry();
            await commerceDbContext.SaveChangesAsync(cancellationToken);

            logger.LogWarning(
                "OxaPay Payment Information unavailable for payment attempt {PaymentAttemptId}: {Error}",
                paymentAttemptId, statusResult.Error.Description);

            return new OxaPayVerificationResult(OxaPayVerificationOutcome.StillPending, order.Id, "Payment provider temporarily unavailable.");
        }

        var status = statusResult.Value;

        if (status.IsStillProcessing)
        {
            attempt.ReleaseForRetry();
            await commerceDbContext.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "OxaPay payment still processing for payment attempt {PaymentAttemptId} (status: {Status}); will retry.",
                paymentAttemptId, status.Status);

            return new OxaPayVerificationResult(OxaPayVerificationOutcome.StillPending, order.Id, "Payment is still being processed.");
        }

        if (!status.IsSuccessfulPayment)
        {
            attempt.MarkFailed(status.Status, status.Status);
            order.MarkFailed();
            RecordAudit(order.Id, "VerificationFailed", "Failed", attempt, status.Status);
            await commerceDbContext.SaveChangesAsync(cancellationToken);
            return new OxaPayVerificationResult(OxaPayVerificationOutcome.Failed, order.Id, status.Status);
        }

        if (status.Amount is not decimal verifiedAmount
            || Math.Abs(verifiedAmount - attempt.ExpectedAmount) > AmountToleranceUsd)
        {
            // OxaPay itself says "paid" but what it actually received doesn't match what HAMBOX
            // priced this order at when it initiated the attempt. Never fulfill on a mismatch, no
            // matter how the transaction is labeled.
            logger.LogError(
                "OxaPay verified amount mismatch for payment attempt {PaymentAttemptId}: expected {ExpectedAmount} USD, OxaPay reported {VerifiedAmount}.",
                paymentAttemptId, attempt.ExpectedAmount, status.Amount);

            attempt.MarkFailed("AMOUNT_MISMATCH", "Verified amount did not match the expected charge.");
            order.MarkFailed();
            RecordAudit(order.Id, "VerificationFailed", "AmountMismatch", attempt, "Verified amount mismatch.");
            await commerceDbContext.SaveChangesAsync(cancellationToken);
            return new OxaPayVerificationResult(OxaPayVerificationOutcome.Failed, order.Id, "Payment verification failed.");
        }

        OrderFulfillmentResult? fulfillmentResult = null;
        await transactionService.ExecuteAsync(async ct =>
        {
            attempt.MarkSucceeded(status.TrackId, verifiedAmount, status.Currency ?? "USD");
            order.RecordPayment("OxaPay", status.TrackId);
            RecordAudit(order.Id, "VerificationSucceeded", "Paid", attempt, status.Status);

            var cart = await commerceDbContext.ShoppingCarts
                .Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.UserId == order.UserId, ct);
            cart?.Clear();

            if (!string.IsNullOrWhiteSpace(attempt.PendingPromotionsJson))
            {
                var appliedPromotions = JsonSerializer.Deserialize<List<AppliedPromotionDto>>(attempt.PendingPromotionsJson) ?? [];
                if (appliedPromotions.Count > 0)
                {
                    await promotionRedemptionService.RedeemAsync(order, appliedPromotions, order.UserId, ct);
                }
            }

            await referralLifecycle.ProcessOrderCompletedAsync(order, ct);

            try
            {
                fulfillmentResult = await fulfillmentService.FulfillMissingAsync(order, ct);
            }
            catch (InvalidOperationException ex)
            {
                logger.LogError(
                    ex, "OxaPay payment attempt {PaymentAttemptId} succeeded but fulfillment failed; will retry.", attempt.Id);
            }

            await commerceDbContext.SaveChangesAsync(ct);
            await catalogDbContext.SaveChangesAsync(ct);
        }, cancellationToken);

        if (fulfillmentResult?.PendingChatDeliveryTickets.Count > 0)
        {
            await fulfillmentService.CreatePendingChatDeliveryTicketsAsync(order, fulfillmentResult.PendingChatDeliveryTickets, cancellationToken);
        }

        if (order.Status != OrderStatus.Completed)
        {
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

        return new OxaPayVerificationResult(OxaPayVerificationOutcome.Succeeded, order.Id, null);
    }

    private async Task<OxaPayVerificationResult> ReportCurrentStateAsync(Guid paymentAttemptId, CancellationToken cancellationToken)
    {
        var existing = await commerceDbContext.PaymentAttempts
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == paymentAttemptId, cancellationToken);

        return existing is null
            ? new OxaPayVerificationResult(OxaPayVerificationOutcome.NotFound, null, null)
            : MapToResult(existing);
    }

    private static OxaPayVerificationResult MapToResult(PaymentAttempt attempt) => attempt.Status switch
    {
        PaymentAttemptStatus.Succeeded => new OxaPayVerificationResult(OxaPayVerificationOutcome.Succeeded, attempt.OrderId, null),
        PaymentAttemptStatus.Failed => new OxaPayVerificationResult(OxaPayVerificationOutcome.Failed, attempt.OrderId, attempt.LastReasonDescription),
        PaymentAttemptStatus.Expired => new OxaPayVerificationResult(OxaPayVerificationOutcome.Failed, attempt.OrderId, "The payment window expired."),
        _ => new OxaPayVerificationResult(OxaPayVerificationOutcome.StillPending, attempt.OrderId, null),
    };

    private void RecordAudit(Guid orderId, string eventType, string status, PaymentAttempt attempt, string? providerMessage)
    {
        var payload = JsonSerializer.Serialize(new
        {
            attempt.PartnerTxId,
            attempt.ProviderTransactionId,
            attempt.ExpectedAmount,
            attempt.ExpectedCurrency,
            attempt.VerifiedAmount,
            attempt.VerifiedCurrency,
            ProviderMessage = providerMessage,
        });

        commerceDbContext.OrderPaymentCallbacks.Add(OrderPaymentCallback.Create(
            orderId, "OxaPay", eventType, status, attempt.ProviderTransactionId, payload));
    }
}
