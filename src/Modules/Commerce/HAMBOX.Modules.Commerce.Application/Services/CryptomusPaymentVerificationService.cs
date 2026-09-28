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

public enum CryptomusVerificationOutcome
{
    Succeeded,
    Failed,
    StillPending,
    NotFound,
}

public sealed record CryptomusVerificationResult(CryptomusVerificationOutcome Outcome, Guid? OrderId, string? Message);

/// <summary>
/// The single, authoritative place a Cryptomus payment attempt is ever resolved from Pending to a
/// terminal state. Invoked from two independent triggers — Cryptomus's webhook and the active-poll
/// re-verification on the result page (plus, once added, a reconciliation sweep) — all of which race
/// safely against each other via the guarded <c>Pending -&gt; Verifying</c> claim below. Mirrors
/// <c>DotPaymentVerificationService</c> exactly; see it for the full rationale of every step.
/// </summary>
public sealed class CryptomusPaymentVerificationService(
    ICommerceDbContext commerceDbContext,
    ICatalogDbContext catalogDbContext,
    ICommerceTransactionService transactionService,
    ICryptomusPaymentGateway cryptomusGateway,
    OrderFulfillmentService fulfillmentService,
    PromotionRedemptionService promotionRedemptionService,
    ReferralLifecycleService referralLifecycle,
    ICommunicationService communicationService,
    IOperationalJobQueue jobQueue,
    ILogger<CryptomusPaymentVerificationService> logger)
{
    private const decimal AmountToleranceUsd = 0.01m;

    public async Task<CryptomusVerificationResult> VerifyAndFinalizeAsync(Guid paymentAttemptId, CancellationToken cancellationToken)
    {
        var attempt = await commerceDbContext.PaymentAttempts.FirstOrDefaultAsync(p => p.Id == paymentAttemptId, cancellationToken);
        if (attempt is null)
        {
            return new CryptomusVerificationResult(CryptomusVerificationOutcome.NotFound, null, null);
        }

        if (attempt.Status != PaymentAttemptStatus.Pending)
        {
            return MapToResult(attempt);
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
            logger.LogError("Cryptomus payment attempt {PaymentAttemptId} references missing order {OrderId}.", attempt.Id, attempt.OrderId);
            attempt.MarkFailed("ORDER_NOT_FOUND", "The associated order could not be found.");
            await commerceDbContext.SaveChangesAsync(cancellationToken);
            return new CryptomusVerificationResult(CryptomusVerificationOutcome.Failed, attempt.OrderId, "Order not found.");
        }

        var statusResult = await cryptomusGateway.GetPaymentInfoByOrderIdAsync(attempt.PartnerTxId, cancellationToken);

        if (statusResult.IsFailure)
        {
            // Transient provider/network failure — release the claim back to Pending so a later
            // webhook or the reconciliation sweep can try again. Never guess.
            attempt.ReleaseForRetry();
            await commerceDbContext.SaveChangesAsync(cancellationToken);

            logger.LogWarning(
                "Cryptomus Payment Info unavailable for payment attempt {PaymentAttemptId}: {Error}",
                paymentAttemptId, statusResult.Error.Description);

            return new CryptomusVerificationResult(CryptomusVerificationOutcome.StillPending, order.Id, "Payment provider temporarily unavailable.");
        }

        var status = statusResult.Value;

        if (status.IsStillProcessing)
        {
            attempt.ReleaseForRetry();
            await commerceDbContext.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "Cryptomus payment still processing for payment attempt {PaymentAttemptId} (status: {Status}); will retry.",
                paymentAttemptId, status.Status);

            return new CryptomusVerificationResult(CryptomusVerificationOutcome.StillPending, order.Id, "Payment is still being processed.");
        }

        if (!status.IsSuccessfulPayment)
        {
            attempt.MarkFailed(status.Status, status.Status);
            order.MarkFailed();
            RecordAudit(order.Id, "VerificationFailed", "Failed", attempt, status.Status);
            await commerceDbContext.SaveChangesAsync(cancellationToken);
            return new CryptomusVerificationResult(CryptomusVerificationOutcome.Failed, order.Id, status.Status);
        }

        if (status.PaymentAmountUsd is not decimal verifiedAmount
            || Math.Abs(verifiedAmount - attempt.ExpectedAmount) > AmountToleranceUsd)
        {
            // Cryptomus itself says "paid" but what it actually received doesn't match what HAMBOX
            // priced this order at when it initiated the attempt. Never fulfill on a mismatch, no
            // matter how the transaction is labeled — "paid_over"/"wrong_amount" both land here.
            logger.LogError(
                "Cryptomus verified amount mismatch for payment attempt {PaymentAttemptId}: expected {ExpectedAmount} USD, Cryptomus reported {VerifiedAmount} USD.",
                paymentAttemptId, attempt.ExpectedAmount, status.PaymentAmountUsd);

            attempt.MarkFailed("AMOUNT_MISMATCH", "Verified amount did not match the expected charge.");
            order.MarkFailed();
            RecordAudit(order.Id, "VerificationFailed", "AmountMismatch", attempt, "Verified amount mismatch.");
            await commerceDbContext.SaveChangesAsync(cancellationToken);
            return new CryptomusVerificationResult(CryptomusVerificationOutcome.Failed, order.Id, "Payment verification failed.");
        }

        OrderFulfillmentResult? fulfillmentResult = null;
        await transactionService.ExecuteAsync(async ct =>
        {
            var providerTransactionId = string.IsNullOrWhiteSpace(status.Uuid) ? attempt.PartnerTxId : status.Uuid;
            attempt.MarkSucceeded(providerTransactionId, verifiedAmount, "USD");
            order.RecordPayment("Cryptomus", providerTransactionId);
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
                    ex, "Cryptomus payment attempt {PaymentAttemptId} succeeded but fulfillment failed; will retry.", attempt.Id);
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

        return new CryptomusVerificationResult(CryptomusVerificationOutcome.Succeeded, order.Id, null);
    }

    private async Task<CryptomusVerificationResult> ReportCurrentStateAsync(Guid paymentAttemptId, CancellationToken cancellationToken)
    {
        var existing = await commerceDbContext.PaymentAttempts
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == paymentAttemptId, cancellationToken);

        return existing is null
            ? new CryptomusVerificationResult(CryptomusVerificationOutcome.NotFound, null, null)
            : MapToResult(existing);
    }

    private static CryptomusVerificationResult MapToResult(PaymentAttempt attempt) => attempt.Status switch
    {
        PaymentAttemptStatus.Succeeded => new CryptomusVerificationResult(CryptomusVerificationOutcome.Succeeded, attempt.OrderId, null),
        PaymentAttemptStatus.Failed => new CryptomusVerificationResult(CryptomusVerificationOutcome.Failed, attempt.OrderId, attempt.LastReasonDescription),
        PaymentAttemptStatus.Expired => new CryptomusVerificationResult(CryptomusVerificationOutcome.Failed, attempt.OrderId, "The payment window expired."),
        _ => new CryptomusVerificationResult(CryptomusVerificationOutcome.StillPending, attempt.OrderId, null),
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
            orderId, "Cryptomus", eventType, status, attempt.ProviderTransactionId, payload));
    }
}
