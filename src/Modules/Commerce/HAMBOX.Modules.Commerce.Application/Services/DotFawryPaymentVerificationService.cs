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
    /// <summary>
    /// Direct Billing resultCodes that are documented, terminal billing outcomes reported
    /// synchronously by DOT itself (Direct Billing API Integration PDF, p.6) — as opposed to "0"
    /// (success, still re-verified rather than trusted alone) and "1000" (processing, never final).
    /// Also the exact set the Transaction Status Notification webhook can send (PDF p.9: 0/1005/1009/1099),
    /// a strict subset of this list.
    /// </summary>
    private static readonly HashSet<string> DefinitiveDirectBillingFailureCodes =
        ["1004", "1005", "1007", "1008", "1009", "1010", "1011", "1013", "1014", "1099"];

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
        // for limited cases" — the Fawry operator/service combo (opId 141) rejects it outright
        // (every production attempt observed resultCode 1005/1008, the check-status call's own
        // error codes, never a real billing result); whether Orange Cash (117)/Vodafone Cash (114)
        // behave the same way is not yet confirmed. dotTransId lookup has no such caveat and is
        // always available once the charge call has returned one, which happens before verification
        // is ever reached — so this branch only actually exercises partnerTransId lookup in the
        // narrow window where a charge call succeeded without yet recording a dotTransId.
        var statusResult = string.IsNullOrWhiteSpace(attempt.ProviderTransactionId)
            ? await dotFawryGateway.CheckTransactionStatusByPartnerTxIdAsync(attempt.PartnerTxId, attempt.OperatorId, cancellationToken)
            : await dotFawryGateway.CheckTransactionStatusByDotTxIdAsync(attempt.ProviderTransactionId, attempt.OperatorId, cancellationToken);

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

        if (status.ResultCode != "0")
        {
            // Per the Check Transaction Status spec, resultCode != "0" means an error occurred in
            // *this check-status call itself* (not found yet, invalid request, internal error) —
            // billingTransactionResultCode is only ever populated once resultCode is "0", so there
            // is no real billing answer to act on here, from this call alone.
            //
            // But the original Direct Billing charge (or a webhook notification) may have already
            // reported a definitive, documented terminal failure for this exact transaction —
            // e.g. "1014 Billing attempts exceeded" — before this check-status call ever ran.
            // Observed in production: a charge that failed synchronously with 1014 never became
            // look-up-able via Check Transaction Status at all (repeatedly "1006 Transaction Not
            // Found"), which without this check would retry forever and leave the customer stuck on
            // "Awaiting Payment" indefinitely for a charge that was never going to succeed. DOT
            // hasn't indexed/won't ever resolve it — that doesn't undo the answer it already gave.
            if (attempt.LastReasonCode is { } lastReasonCode && DefinitiveDirectBillingFailureCodes.Contains(lastReasonCode))
            {
                attempt.MarkFailed(attempt.LastReasonCode, attempt.LastReasonDescription);
                order.MarkFailed();
                RecordAudit(order.Id, "VerificationFailed", "Failed", attempt, attempt.LastReasonDescription);
                await commerceDbContext.SaveChangesAsync(cancellationToken);
                return new DotFawryVerificationResult(DotFawryVerificationOutcome.Failed, order.Id, attempt.LastReasonDescription);
            }

            // Otherwise genuinely inconclusive — release for retry exactly like a transient
            // provider failure, never guess "failed" from a check-status-layer error alone.
            attempt.ReleaseForRetry();
            await commerceDbContext.SaveChangesAsync(cancellationToken);

            logger.LogWarning(
                "DOT Fawry Check Transaction Status inconclusive for payment attempt {PaymentAttemptId}: resultCode {ResultCode} ({ResultDesc}).",
                paymentAttemptId, status.ResultCode, status.ResultDesc);

            return new DotFawryVerificationResult(DotFawryVerificationOutcome.StillPending, order.Id, status.ResultDesc);
        }

        if (!status.IsSuccessfulTransaction)
        {
            attempt.MarkFailed(status.BillingTransactionResultCode, status.BillingTransactionResultDesc);
            order.MarkFailed();
            RecordAudit(order.Id, "VerificationFailed", "Failed", attempt, status.BillingTransactionResultDesc);
            await commerceDbContext.SaveChangesAsync(cancellationToken);
            return new DotFawryVerificationResult(DotFawryVerificationOutcome.Failed, order.Id, status.BillingTransactionResultDesc);
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
