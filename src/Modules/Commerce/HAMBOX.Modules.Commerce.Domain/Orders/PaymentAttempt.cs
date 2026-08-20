using HAMBOX.Domain.Entities;
using HAMBOX.Modules.Commerce.Domain.Enums;

namespace HAMBOX.Modules.Commerce.Domain.Orders;

/// <summary>
/// Tracks one asynchronous, redirect-based payment attempt (e.g. DOT direct-carrier-billing)
/// against an <see cref="Order"/>. An order can have more than one attempt over time (retry after
/// failure/expiry), so this is a child record, not a 1:1 extension of <c>Order</c>.
/// <para>
/// <see cref="PartnerTxId"/> is HAMBOX's own generated identifier and is unique per
/// <see cref="Provider"/> — it is what the database uses to guarantee a given attempt is only ever
/// initiated once and only ever finalized once, independent of anything the provider or the
/// customer's browser reports back.
/// </para>
/// </summary>
public sealed class PaymentAttempt : Entity
{
    private PaymentAttempt()
    {
    }

    private PaymentAttempt(
        Guid id,
        Guid orderId,
        string provider,
        string partnerTxId,
        string operatorId,
        string serviceId,
        decimal expectedAmount,
        string expectedCurrency,
        DateTimeOffset expiresOnUtc,
        string? pendingPromotionsJson)
        : base(id)
    {
        OrderId = orderId;
        Provider = provider;
        PartnerTxId = partnerTxId;
        OperatorId = operatorId;
        ServiceId = serviceId;
        ExpectedAmount = expectedAmount;
        ExpectedCurrency = expectedCurrency;
        Status = PaymentAttemptStatus.Pending;
        ExpiresOnUtc = expiresOnUtc;
        PendingPromotionsJson = pendingPromotionsJson;
    }

    public Guid OrderId { get; private set; }

    /// <summary>The provider key, e.g. "Dot". Matches <c>Order.PaymentProvider</c> once settled.</summary>
    public string Provider { get; private set; } = string.Empty;

    /// <summary>HAMBOX-generated unique transaction id sent to the provider as <c>partner_txid</c>.</summary>
    public string PartnerTxId { get; private set; } = string.Empty;

    /// <summary>The provider's own transaction id (DOT's <c>dot_txid</c>), known once the redirect/notification arrives.</summary>
    public string? ProviderTransactionId { get; private set; }

    public PaymentAttemptStatus Status { get; private set; }

    /// <summary>What HAMBOX expected to charge when the attempt was initiated. Authoritative for verification — never overwritten by anything a callback or webhook claims.</summary>
    public decimal ExpectedAmount { get; private set; }

    public string ExpectedCurrency { get; private set; } = string.Empty;

    /// <summary>What the provider's own status-check response confirmed. Populated only after a successful, authoritative verification call.</summary>
    public decimal? VerifiedAmount { get; private set; }

    public string? VerifiedCurrency { get; private set; }

    /// <summary>Operator/service context captured at initiation, for consistency checks against later callbacks/notifications.</summary>
    public string OperatorId { get; private set; } = string.Empty;

    public string ServiceId { get; private set; } = string.Empty;

    /// <summary>Masked mobile number (last 4 digits only) captured from the redirect, for support/reconciliation. Never the full MSISDN.</summary>
    public string? MaskedMsisdn { get; private set; }

    /// <summary>
    /// A customer-facing reference code the provider hands back for the customer to complete
    /// payment elsewhere (e.g. DOT/Fawry's Direct Billing response <c>extraField1</c>, the Fawry
    /// reference number entered into the Fawry wallet). Not used by every provider — <c>null</c>
    /// when the provider has no such concept.
    /// </summary>
    public string? ProviderReferenceCode { get; private set; }

    public string? LastReasonCode { get; private set; }

    public string? LastReasonDescription { get; private set; }

    /// <summary>Serialized applied-promotions snapshot from initiation, redeemed only on success (mirrors reserve-then-commit for inventory).</summary>
    public string? PendingPromotionsJson { get; private set; }

    public DateTimeOffset ExpiresOnUtc { get; private set; }

    public DateTimeOffset? CompletedOnUtc { get; private set; }

    /// <summary>
    /// EF concurrency token guarding the <see cref="PaymentAttemptStatus.Pending"/> -&gt;
    /// <see cref="PaymentAttemptStatus.Verifying"/> claim: the browser callback, the provider
    /// webhook, and the reconciliation sweep can all try to claim the same attempt at once, and
    /// only one write is allowed to win. A losing writer gets a <c>DbUpdateConcurrencyException</c>
    /// and treats that exactly like losing the claim to a caller who got there first.
    /// </summary>
    public byte[] RowVersion { get; private set; } = [];

    public static PaymentAttempt CreatePendingDot(
        Guid orderId,
        string partnerTxId,
        string operatorId,
        string serviceId,
        decimal expectedAmount,
        string expectedCurrency,
        DateTimeOffset expiresOnUtc,
        string? pendingPromotionsJson)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(partnerTxId);
        ArgumentException.ThrowIfNullOrWhiteSpace(operatorId);
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedCurrency);

        return new PaymentAttempt(
            Guid.NewGuid(),
            orderId,
            "Dot",
            partnerTxId,
            operatorId,
            serviceId,
            expectedAmount,
            expectedCurrency,
            expiresOnUtc,
            pendingPromotionsJson);
    }

    /// <summary>
    /// Creates a Pending attempt for the DOT Fawry Direct Billing product — a distinct DOT
    /// integration from the carrier-billing OTP flow (<see cref="CreatePendingDot"/>), sharing this
    /// entity because the lifecycle (Pending -&gt; Verifying -&gt; Succeeded/Failed/Expired) and
    /// claim-guarded verification pattern are identical.
    /// </summary>
    public static PaymentAttempt CreatePendingDotFawry(
        Guid orderId,
        string partnerTxId,
        string operatorId,
        string serviceId,
        decimal expectedAmount,
        string expectedCurrency,
        string maskedMsisdn,
        DateTimeOffset expiresOnUtc,
        string? pendingPromotionsJson)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(partnerTxId);
        ArgumentException.ThrowIfNullOrWhiteSpace(operatorId);
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedCurrency);
        ArgumentException.ThrowIfNullOrWhiteSpace(maskedMsisdn);

        return new PaymentAttempt(
            Guid.NewGuid(),
            orderId,
            "DotFawry",
            partnerTxId,
            operatorId,
            serviceId,
            expectedAmount,
            expectedCurrency,
            expiresOnUtc,
            pendingPromotionsJson)
        {
            MaskedMsisdn = maskedMsisdn,
        };
    }

    /// <summary>
    /// Records provider/callback context as it becomes known. Safe to call repeatedly (e.g. once
    /// from the browser callback, again from the webhook) — it never changes <see cref="Status"/>.
    /// </summary>
    public void RecordProviderContext(string? providerTransactionId, string? maskedMsisdn, string? reasonCode, string? reasonDescription)
    {
        if (!string.IsNullOrWhiteSpace(providerTransactionId))
        {
            ProviderTransactionId = providerTransactionId;
        }

        if (!string.IsNullOrWhiteSpace(maskedMsisdn))
        {
            MaskedMsisdn = maskedMsisdn;
        }

        LastReasonCode = reasonCode;
        LastReasonDescription = reasonDescription;
    }

    /// <summary>Records the provider's customer-facing reference code (e.g. a Fawry reference number). Safe to call repeatedly.</summary>
    public void RecordProviderReferenceCode(string? providerReferenceCode)
    {
        if (!string.IsNullOrWhiteSpace(providerReferenceCode))
        {
            ProviderReferenceCode = providerReferenceCode;
        }
    }

    /// <summary>
    /// Claims this attempt for verification. Only legal from <see cref="PaymentAttemptStatus.Pending"/>
    /// — callers must persist this via <c>SaveChangesAsync</c> immediately and treat a
    /// <c>DbUpdateConcurrencyException</c> as "someone else claimed it first," not a real error.
    /// </summary>
    public void BeginVerification()
    {
        if (Status != PaymentAttemptStatus.Pending)
        {
            throw new InvalidOperationException(
                $"Cannot begin verification for payment attempt {Id} from status {Status}.");
        }

        Status = PaymentAttemptStatus.Verifying;
    }

    /// <summary>
    /// Marks the attempt as succeeded after an authoritative Check-Transaction-Status call.
    /// Only legal from <see cref="PaymentAttemptStatus.Verifying"/> — the caller must have already
    /// won the guarded claim (see the module's <c>ExecuteUpdate</c> claim pattern) before calling this.
    /// </summary>
    public void MarkSucceeded(string providerTransactionId, decimal verifiedAmount, string verifiedCurrency)
    {
        if (Status != PaymentAttemptStatus.Verifying)
        {
            throw new InvalidOperationException(
                $"Cannot mark payment attempt {Id} succeeded from status {Status}.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(providerTransactionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(verifiedCurrency);

        ProviderTransactionId = providerTransactionId;
        VerifiedAmount = verifiedAmount;
        VerifiedCurrency = verifiedCurrency;
        Status = PaymentAttemptStatus.Succeeded;
        CompletedOnUtc = DateTimeOffset.UtcNow;
    }

    /// <summary>Releases a claimed attempt back to Pending after a transient provider failure (network/timeout/5xx) — never after a real answer from DOT.</summary>
    public void ReleaseForRetry()
    {
        if (Status != PaymentAttemptStatus.Verifying)
        {
            throw new InvalidOperationException(
                $"Cannot release payment attempt {Id} for retry from status {Status}.");
        }

        Status = PaymentAttemptStatus.Pending;
    }

    public void MarkFailed(string? reasonCode, string? reasonDescription)
    {
        if (Status != PaymentAttemptStatus.Verifying)
        {
            throw new InvalidOperationException(
                $"Cannot mark payment attempt {Id} failed from status {Status}.");
        }

        LastReasonCode = reasonCode;
        LastReasonDescription = reasonDescription;
        Status = PaymentAttemptStatus.Failed;
        CompletedOnUtc = DateTimeOffset.UtcNow;
    }

    /// <summary>Reservation-window sweep: never resolved by callback, webhook, or reconciliation before it expired.</summary>
    public void MarkExpired()
    {
        if (Status is not (PaymentAttemptStatus.Pending or PaymentAttemptStatus.Verifying))
        {
            throw new InvalidOperationException(
                $"Cannot mark payment attempt {Id} expired from status {Status}.");
        }

        Status = PaymentAttemptStatus.Expired;
        CompletedOnUtc = DateTimeOffset.UtcNow;
    }
}
