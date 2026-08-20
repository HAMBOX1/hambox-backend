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

public enum DotFawryVerificationOutcome
{
    Succeeded,
    Failed,
    StillPending,
    NotFound,
}

public sealed record DotFawryVerificationResult(DotFawryVerificationOutcome Outcome, Guid? OrderId, string? Message);

/// <summary>
/// The single, authoritative place a DOT Fawry payment attempt is ever resolved from Pending to a
/// terminal state. Invoked from two independent triggers — DOT's server-to-server notification
/// webhook and the background reconciliation sweep (there is no browser redirect for this
/// server-to-server product, unlike the carrier-billing OTP flow) — both of which race safely
/// against each other via the guarded <c>Pending -&gt; Verifying</c> claim below, and both of which
/// get the same authoritative answer because neither trusts anything except this service's own call
/// to DOT's Check Transaction Status API.
/// <para>
/// This is a sibling of <c>DotPaymentVerificationService</c> for a different DOT product — kept as
/// a separate implementation rather than a shared/parameterized one so the existing carrier-billing
/// integration is never touched by changes here (and vice versa). One structural difference from
/// that sibling: DOT's Direct Billing Check Transaction Status response does not return
/// amount/currency, so there is no independent charged-amount to cross-verify against what HAMBOX
/// expected — only the transaction's success/failure result code is authoritative here.
/// </para>
/// </summary>
public sealed class DotFawryPaymentVerificationService(
    ICommerceDbContext commerceDbContext,
    ICatalogDbContext catalogDbContext,
    ICommerceTransactionService transactionService,
    IDotFawryPaymentGateway dotFawryGateway,
    OrderFulfillmentService fulfillmentService,
    PromotionRedemptionService promotionRedemptionService,
    ReferralLifecycleService referralLifecycle,
    ICommunicationService communicationService,
    IOperationalJobQueue jobQueue,
    ILogger<DotFawryPaymentVerificationService> logger)
{
    public async Task<DotFawryVerificationResult> VerifyAndFinalizeAsync(Guid paymentAttemptId, CancellationToken cancellationToken)
    {
        var attempt = await commerceDbContext.PaymentAttempts.FirstOrDefaultAsync(p => p.Id == paymentAttemptId, cancellationToken);
        if (attempt is null)
        {
            return new DotFawryVerificationResult(DotFawryVerificationOutcome.NotFound, null, null);
        }

        if (attempt.Status != PaymentAttemptStatus.Pending)
        {
            // Already terminal, or already being verified by a concurrent caller — either way,
            // safe to report back as the attempt's current state rather than treat as an error.
            // This is what makes duplicate notification retries idempotent.
            return MapToResult(attempt);
        }

        attempt.BeginVerification();

        try
        {
            await commerceDbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Lost the race to claim this attempt to a concurrent caller (webhook vs. reconciliation
            // sweep landing at the same instant) — re-read whatever they left it as.
            return await ReportCurrentStateAsync(paymentAttemptId, cancellationToken);
        }

        var order = await commerceDbContext.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == attempt.OrderId, cancellationToken);

        if (order is null)
        {
            logger.LogError("DOT Fawry payment attempt {PaymentAttemptId} references missing order {OrderId}.", attempt.Id, attempt.OrderId);
            attempt.MarkFailed("ORDER_NOT_FOUND", "The associated order could not be found.");
            await commerceDbContext.SaveChangesAsync(cancellationToken);
            return new DotFawryVerificationResult(DotFawryVerificationOutcome.Failed, attempt.OrderId, "Order not found.");
        }

        // DOT's Check Transaction Status spec states the partnerTransId lookup is "available only
        // for limited cases" — this operator/service combo rejects it outright (every production
        // attempt observed resultCode 1005/1008, the check-status call's own error codes, never a
        // real billing result). dotTransId lookup has no such caveat and is always available once
        // the charge call has returned one, which happens before verification is ever reached.
        var statusResult = string.IsNullOrWhiteSpace(attempt.ProviderTransactionId)
            ? await dotFawryGateway.CheckTransactionStatusByPartnerTxIdAsync(attempt.PartnerTxId, cancellationToken)
            : await dotFawryGateway.CheckTransactionStatusByDotTxIdAsync(attempt.ProviderTransactionId, cancellationToken);

        if (statusResult.IsFailure)
        {
            // Transient provider/network failure — release the claim back to Pending so the
            // notification webhook (if it retries) or the reconciliation sweep can try again. Never guess.
            attempt.ReleaseForRetry();
            await commerceDbContext.SaveChangesAsync(cancellationToken);

            logger.LogWarning(
                "DOT Fawry Check Transaction Status unavailable for payment attempt {PaymentAttemptId}: {Error}",
                paymentAttemptId, statusResult.Error.Description);

            return new DotFawryVerificationResult(DotFawryVerificationOutcome.StillPending, order.Id, "Payment provider temporarily unavailable.");
        }

        var status = statusResult.Value;

        if (status.IsStillProcessing)
        {
            // resultCode 1000 at the check-status layer: DOT/the operator itself hasn't resolved
            // this yet. Release back to Pending — never mark failed or succeeded on "still processing".
            attempt.ReleaseForRetry();
            await commerceDbContext.SaveChangesAsync(cancellationToken);
            return new DotFawryVerificationResult(DotFawryVerificationOutcome.StillPending, order.Id, status.BillingTransactionResultDesc);
        }

        if (!status.IsSuccessfulTransaction)
        {
            attempt.MarkFailed(status.BillingTransactionResultCode ?? status.ResultCode, status.BillingTransactionResultDesc ?? status.ResultDesc);
            order.MarkFailed();
            RecordAudit(order.Id, "VerificationFailed", "Failed", attempt, status.BillingTransactionResultDesc ?? status.ResultDesc);
            await commerceDbContext.SaveChangesAsync(cancellationToken);
            return new DotFawryVerificationResult(DotFawryVerificationOutcome.Failed, order.Id, status.BillingTransactionResultDesc ?? status.ResultDesc);
        }

        await transactionService.ExecuteAsync(async ct =>
        {
            var providerTransactionId = attempt.ProviderTransactionId ?? status.DotTransId ?? attempt.PartnerTxId;

            // DOT's Direct Billing Check Transaction Status response carries no amount/currency to
            // independently verify against (see class remarks) — the expected values recorded at
            // initiation (server-priced, never client-supplied) are the only figures available, so
            // they are what's persisted as "verified" here. This is a real gap versus the OTP
            // product's amount cross-check, not an oversight.
            attempt.MarkSucceeded(providerTransactionId, attempt.ExpectedAmount, attempt.ExpectedCurrency);
            order.RecordPayment("DotFawry", providerTransactionId);
            RecordAudit(order.Id, "VerificationSucceeded", "Paid", attempt, status.BillingTransactionResultDesc ?? status.ResultDesc);

            if (!string.IsNullOrWhiteSpace(attempt.PendingPromotionsJson))
            {
                var appliedPromotions = JsonSerializer.Deserialize<List<AppliedPromotionDto>>(attempt.PendingPromotionsJson) ?? [];
                if (appliedPromotions.Count > 0)
                {
                    await promotionRedemptionService.RedeemAsync(order, appliedPromotions, order.UserId, ct);
                }
            }

            await referralLifecycle.ProcessOrderCompletedAsync(order, ct);

            // Only now — after authoritative confirmation — does inventory ever get reserved and
            // committed for this order. Digital codes were never touched while the customer was off
            // paying at Fawry.
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
                    ex, "DOT Fawry payment attempt {PaymentAttemptId} succeeded but fulfillment failed; will retry.", attempt.Id);
            }

            await commerceDbContext.SaveChangesAsync(ct);
            await catalogDbContext.SaveChangesAsync(ct);
        }, cancellationToken);

        if (order.Status != OrderStatus.Completed)
        {
            // Paid but not (yet, or fully) delivered — same safety net an admin-triggered
            // fulfillment retry uses, not a DOT-Fawry-specific path.
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

        return new DotFawryVerificationResult(DotFawryVerificationOutcome.Succeeded, order.Id, null);
    }

    private async Task<DotFawryVerificationResult> ReportCurrentStateAsync(Guid paymentAttemptId, CancellationToken cancellationToken)
    {
        var existing = await commerceDbContext.PaymentAttempts
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == paymentAttemptId, cancellationToken);

        return existing is null
            ? new DotFawryVerificationResult(DotFawryVerificationOutcome.NotFound, null, null)
            : MapToResult(existing);
    }

    private static DotFawryVerificationResult MapToResult(PaymentAttempt attempt) => attempt.Status switch
    {
        PaymentAttemptStatus.Succeeded => new DotFawryVerificationResult(DotFawryVerificationOutcome.Succeeded, attempt.OrderId, null),
        PaymentAttemptStatus.Failed => new DotFawryVerificationResult(DotFawryVerificationOutcome.Failed, attempt.OrderId, attempt.LastReasonDescription),
        PaymentAttemptStatus.Expired => new DotFawryVerificationResult(DotFawryVerificationOutcome.Failed, attempt.OrderId, "The payment window expired."),
        _ => new DotFawryVerificationResult(DotFawryVerificationOutcome.StillPending, attempt.OrderId, null),
    };

    private void RecordAudit(Guid orderId, string eventType, string status, PaymentAttempt attempt, string? providerMessage)
    {
        var payload = JsonSerializer.Serialize(new
        {
            attempt.PartnerTxId,
            attempt.ProviderTransactionId,
            attempt.ProviderReferenceCode,
            attempt.OperatorId,
            attempt.ServiceId,
            attempt.ExpectedAmount,
            attempt.ExpectedCurrency,
            attempt.MaskedMsisdn,
            ProviderMessage = providerMessage,
        });

        commerceDbContext.OrderPaymentCallbacks.Add(OrderPaymentCallback.Create(
            orderId, "DotFawry", eventType, status, attempt.ProviderTransactionId, payload));
    }
}
