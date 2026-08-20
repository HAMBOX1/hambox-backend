using HAMBOX.Domain.Entities;

namespace HAMBOX.Modules.Suppliers.Domain.Fulfillments;

/// <summary>
/// Tracks one automated-supplier purchase attempt for the shortfall quantity of a single paid
/// <c>OrderItem</c> that HAMBOX's own manual inventory could not fully cover. This is the recovery
/// record a background worker (or a crash-restarted process) uses to resume, reconcile, or safely
/// retry an in-flight external purchase without ever placing a duplicate order with the supplier.
/// </summary>
/// <remarks>
/// Provider-agnostic by design: nothing here names Bamboo or any other supplier. <see cref="SupplierId"/>
/// and <see cref="SupplierProductMappingId"/> point at the existing <c>Supplier</c>/<c>SupplierProductMapping</c>
/// entities, and <see cref="ProviderOrderId"/>/<see cref="ProviderAccountId"/> are opaque strings so a
/// future provider with a non-numeric or differently-shaped reference scheme needs no schema change.
/// <see cref="OrderId"/>/<see cref="OrderItemId"/> are logical references to Commerce's <c>Order</c>/
/// <c>OrderItem</c> — no cross-schema FK, consistent with the rest of this module.
/// </remarks>
public sealed class SupplierFulfillment : AggregateRoot, IAuditable
{
    private SupplierFulfillment()
    {
    }

    private SupplierFulfillment(
        Guid id,
        Guid orderId,
        Guid orderItemId,
        Guid supplierId,
        Guid supplierProductMappingId,
        int requestedQuantity,
        Guid hamboxReferenceId)
        : base(id)
    {
        OrderId = orderId;
        OrderItemId = orderItemId;
        SupplierId = supplierId;
        SupplierProductMappingId = supplierProductMappingId;
        RequestedQuantity = requestedQuantity;
        HamboxReferenceId = hamboxReferenceId;
        Status = SupplierFulfillmentStatus.Pending;
        DeliveredQuantity = 0;
        Attempts = 0;
    }

    public Guid OrderId { get; private set; }
    public Guid OrderItemId { get; private set; }
    public Guid SupplierId { get; private set; }
    public Guid SupplierProductMappingId { get; private set; }

    /// <summary>The exact shortfall quantity this attempt was created to cover — never exceeded.</summary>
    public int RequestedQuantity { get; private set; }

    /// <summary>How many units the provider actually confirmed as delivered. 0 until a terminal state.</summary>
    public int DeliveredQuantity { get; private set; }

    /// <summary>
    /// The provider-facing idempotency/client-reference identifier (e.g. Bamboo's <c>RequestId</c>).
    /// Generated exactly once, at <see cref="Create"/>, and reused verbatim on every retry of this
    /// attempt — a new value is never generated for the same shortfall while this row can still be
    /// resolved or retried. A new shortfall (e.g. the remainder of a <see cref="SupplierFulfillmentStatus.PartialFailed"/>
    /// attempt) is a new <see cref="SupplierFulfillment"/> row with its own reference, not a mutation
    /// of this one.
    /// </summary>
    public Guid HamboxReferenceId { get; private set; }

    /// <summary>The provider's own order/transaction identifier, once known. Opaque, provider-defined.</summary>
    public string? ProviderOrderId { get; private set; }

    /// <summary>The provider account/funding-source reference used for this purchase, once resolved.</summary>
    public string? ProviderAccountId { get; private set; }

    public SupplierFulfillmentStatus Status { get; private set; }

    /// <summary>Number of times this attempt has been claimed for submission (bounded-retry accounting for the caller).</summary>
    public int Attempts { get; private set; }

    public SupplierFulfillmentFailureCategory? FailureCategory { get; private set; }

    /// <summary>Short, safe failure summary — never a raw provider payload, never secret material.</summary>
    public string? FailureDetail { get; private set; }

    public DateTimeOffset? SubmittedOnUtc { get; private set; }
    public DateTimeOffset? CompletedOnUtc { get; private set; }
    public DateTimeOffset? LastReconciledOnUtc { get; private set; }

    /// <summary>
    /// EF optimistic-concurrency token. This is the actual double-purchase guard: two workers may both
    /// load the same <see cref="SupplierFulfillmentStatus.Pending"/>/<see cref="SupplierFulfillmentStatus.Unknown"/>
    /// row and both call <see cref="Claim"/> in memory, but only the first <c>SaveChangesAsync</c> to
    /// reach the database succeeds — the second fails with <c>DbUpdateConcurrencyException</c> because
    /// this token changed underneath it. Callers MUST treat that exception as "another worker won the
    /// claim, skip" and MUST NOT retry the claim in the same pass. The external provider call must only
    /// ever be made after <see cref="Claim"/>'s <c>SaveChangesAsync</c> has committed successfully.
    /// </summary>
    public byte[] RowVersion { get; private set; } = [];

    public string? CreatedBy { get; set; }
    public string? ModifiedBy { get; set; }

    /// <summary>Units still outstanding for this attempt. Only meaningful once terminal.</summary>
    public int RemainingQuantity => RequestedQuantity - DeliveredQuantity;

    public bool IsTerminal =>
        Status is SupplierFulfillmentStatus.Succeeded or SupplierFulfillmentStatus.PartialFailed or SupplierFulfillmentStatus.Failed;

    public static SupplierFulfillment Create(
        Guid orderId,
        Guid orderItemId,
        Guid supplierId,
        Guid supplierProductMappingId,
        int requestedQuantity)
    {
        if (orderId == Guid.Empty)
        {
            throw new ArgumentException("Order id is required.", nameof(orderId));
        }

        if (orderItemId == Guid.Empty)
        {
            throw new ArgumentException("Order item id is required.", nameof(orderItemId));
        }

        if (supplierId == Guid.Empty)
        {
            throw new ArgumentException("Supplier id is required.", nameof(supplierId));
        }

        if (supplierProductMappingId == Guid.Empty)
        {
            throw new ArgumentException("Supplier product mapping id is required.", nameof(supplierProductMappingId));
        }

        if (requestedQuantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(requestedQuantity), "Requested quantity must be greater than zero.");
        }

        return new SupplierFulfillment(
            Guid.NewGuid(),
            orderId,
            orderItemId,
            supplierId,
            supplierProductMappingId,
            requestedQuantity,
            Guid.NewGuid());
    }

    /// <summary>
    /// Claims this attempt for submission to the provider. Only valid from <see cref="SupplierFulfillmentStatus.Pending"/>
    /// (first attempt) or <see cref="SupplierFulfillmentStatus.Unknown"/> (retry after an ambiguous
    /// failure, once reconciliation has confirmed the provider does not already have this
    /// <see cref="HamboxReferenceId"/>). See the <see cref="RowVersion"/> remarks for the concurrency
    /// contract this method depends on — this method alone does not guarantee exclusivity, persisting
    /// it via EF's concurrency check does.
    /// </summary>
    public void Claim()
    {
        if (Status != SupplierFulfillmentStatus.Pending && Status != SupplierFulfillmentStatus.Unknown)
        {
            throw new InvalidOperationException($"Cannot claim a supplier fulfillment attempt in status '{Status}'.");
        }

        Status = SupplierFulfillmentStatus.Submitting;
        Attempts++;
    }

    /// <summary>The provider confirmed acceptance of the purchase request; outcome still pending.</summary>
    public void MarkSubmitted(string providerOrderId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerOrderId);

        if (Status != SupplierFulfillmentStatus.Submitting)
        {
            throw new InvalidOperationException($"Cannot mark submitted from status '{Status}'.");
        }

        ProviderOrderId = providerOrderId.Trim();
        Status = SupplierFulfillmentStatus.Submitted;
        SubmittedOnUtc = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// The submission call's outcome could not be determined (timeout / network failure with no
    /// definite response). The caller must resolve this via the provider's status-lookup API — never
    /// by re-submitting — before this attempt can move to any other state.
    /// </summary>
    public void MarkUnknown()
    {
        if (Status != SupplierFulfillmentStatus.Submitting)
        {
            throw new InvalidOperationException($"Cannot mark unknown from status '{Status}'.");
        }

        Status = SupplierFulfillmentStatus.Unknown;
    }

    /// <summary>Records that a reconciliation poll ran but did not yet resolve this attempt to a terminal state.</summary>
    public void RecordReconciliationAttempt() => LastReconciledOnUtc = DateTimeOffset.UtcNow;

    public void MarkSucceeded(int deliveredQuantity, string? providerOrderId = null)
    {
        EnsureResolvable();

        if (deliveredQuantity != RequestedQuantity)
        {
            throw new ArgumentOutOfRangeException(
                nameof(deliveredQuantity),
                "A fully succeeded attempt must deliver exactly the requested quantity — use MarkPartialFailed otherwise.");
        }

        SetProviderOrderIdIfKnown(providerOrderId);
        DeliveredQuantity = deliveredQuantity;
        Status = SupplierFulfillmentStatus.Succeeded;
        CompletedOnUtc = DateTimeOffset.UtcNow;
        LastReconciledOnUtc = CompletedOnUtc;
    }

    public void MarkPartialFailed(int deliveredQuantity, string? providerOrderId = null)
    {
        EnsureResolvable();

        if (deliveredQuantity <= 0 || deliveredQuantity >= RequestedQuantity)
        {
            throw new ArgumentOutOfRangeException(
                nameof(deliveredQuantity),
                "A partial failure must deliver more than zero and fewer than the requested quantity.");
        }

        SetProviderOrderIdIfKnown(providerOrderId);
        DeliveredQuantity = deliveredQuantity;
        Status = SupplierFulfillmentStatus.PartialFailed;
        CompletedOnUtc = DateTimeOffset.UtcNow;
        LastReconciledOnUtc = CompletedOnUtc;
    }

    public void MarkFailed(SupplierFulfillmentFailureCategory failureCategory, string? failureDetail, string? providerOrderId = null)
    {
        EnsureResolvable();

        SetProviderOrderIdIfKnown(providerOrderId);
        DeliveredQuantity = 0;
        Status = SupplierFulfillmentStatus.Failed;
        FailureCategory = failureCategory;
        FailureDetail = string.IsNullOrWhiteSpace(failureDetail)
            ? null
            : failureDetail.Trim()[..Math.Min(failureDetail.Trim().Length, 500)];
        CompletedOnUtc = DateTimeOffset.UtcNow;
        LastReconciledOnUtc = CompletedOnUtc;
    }

    /// <summary>
    /// Records the provider's own order/transaction reference the first time it becomes known — which
    /// may happen at submission time, or only later during reconciliation of a <see cref="SupplierFulfillmentStatus.Unknown"/>
    /// or crash-interrupted attempt. Once set, the reference is immutable: a provider (or a bug) ever
    /// reporting a <em>different</em> reference for the same attempt is a correctness/security-relevant
    /// inconsistency, not something to silently overwrite.
    /// </summary>
    private void SetProviderOrderIdIfKnown(string? providerOrderId)
    {
        if (string.IsNullOrWhiteSpace(providerOrderId))
        {
            return;
        }

        var trimmed = providerOrderId.Trim();
        if (ProviderOrderId is not null && ProviderOrderId != trimmed)
        {
            throw new InvalidOperationException(
                $"Provider order id for this attempt is already recorded as '{ProviderOrderId}' — refusing to overwrite with '{trimmed}'.");
        }

        ProviderOrderId = trimmed;
    }

    private void EnsureResolvable()
    {
        if (Status is not (SupplierFulfillmentStatus.Submitting or SupplierFulfillmentStatus.Submitted or SupplierFulfillmentStatus.Unknown))
        {
            throw new InvalidOperationException($"Cannot resolve a supplier fulfillment attempt in status '{Status}'.");
        }
    }
}
