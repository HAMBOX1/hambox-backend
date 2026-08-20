using HAMBOX.Modules.Suppliers.Domain.Fulfillments;
using HAMBOX.SharedKernel.Results;

namespace HAMBOX.Modules.Suppliers.Application.Abstractions;

/// <summary>
/// The one entry point a future caller (Commerce's order-fulfillment flow, or an admin action) uses to
/// drive an automated-supplier purchase for a shortfall quantity. Nothing here — or in any
/// implementation — names a specific provider; provider-specific behavior lives entirely behind
/// <see cref="ISupplierProvider"/>, resolved via <see cref="ISupplierProviderRegistry"/> by
/// <c>Supplier.ProviderType</c>. Adding a second automated supplier never touches this interface, its
/// implementation, or the background worker that drives it.
/// </summary>
/// <remarks>
/// This module has no reference to Commerce, so it cannot itself validate that
/// <see cref="SupplierFulfillmentRequest.RequestedQuantity"/> does not exceed the order item's own
/// quantity/shortfall — that authoritative check is the caller's responsibility (Commerce owns
/// <c>OrderItem</c>). What this service DOES enforce, unconditionally, regardless of caller: the
/// requested quantity is positive, the supplier exists/is enabled, the product mapping exists/is
/// active/actually belongs to the named supplier, and no external call is ever made for an order that
/// this service has not independently confirmed is claimable (see <see cref="ProcessAsync"/>).
/// </remarks>
public interface ISupplierFulfillmentService
{
    /// <summary>
    /// Gets an existing, still-open <see cref="SupplierFulfillment"/> for this exact
    /// (order, order item, supplier, mapping) scope if one exists, or creates a new one — never both.
    /// This is the idempotency seam: calling this twice for the same in-flight shortfall reuses the
    /// same <see cref="SupplierFulfillment.HamboxReferenceId"/> rather than creating a parallel attempt.
    /// A brand-new attempt is only created when no non-terminal attempt exists for the scope (including
    /// the deliberate case of following up a <see cref="SupplierFulfillmentStatus.PartialFailed"/>
    /// attempt's remaining quantity, which — by design — gets its own new reference; see
    /// <see cref="SupplierFulfillment.HamboxReferenceId"/>'s remarks).
    /// </summary>
    Task<Result<SupplierFulfillmentOutcome>> RequestFulfillmentAsync(
        SupplierFulfillmentRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Claims and submits one <see cref="SupplierFulfillmentStatus.Pending"/> or
    /// <see cref="SupplierFulfillmentStatus.Unknown"/> attempt. The external provider call is made
    /// strictly after the claim's optimistic-concurrency write has committed — if another worker won
    /// the claim first, this returns <see cref="Errors.SupplierErrors.ConcurrentClaimLost"/> and never
    /// calls the provider. A no-op (returns the current outcome, unchanged) for any other status.
    /// </summary>
    Task<Result<SupplierFulfillmentOutcome>> ProcessAsync(Guid fulfillmentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Queries the provider's status for a <see cref="SupplierFulfillmentStatus.Submitting"/> (crash
    /// recovery — see remarks), <see cref="SupplierFulfillmentStatus.Submitted"/>, or
    /// <see cref="SupplierFulfillmentStatus.Unknown"/> attempt and safely transitions it. Never performs
    /// a second purchase call. A no-op for any other status.
    /// </summary>
    Task<Result<SupplierFulfillmentOutcome>> ReconcileAsync(Guid fulfillmentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// One sweep: claims+submits due <see cref="SupplierFulfillmentStatus.Pending"/> attempts,
    /// reconciles due <see cref="SupplierFulfillmentStatus.Submitting"/>/<see cref="SupplierFulfillmentStatus.Submitted"/>/<see cref="SupplierFulfillmentStatus.Unknown"/>
    /// attempts, and creates a follow-up attempt for any <see cref="SupplierFulfillmentStatus.PartialFailed"/>
    /// attempt with <see cref="SupplierFulfillment.RemainingQuantity"/> &gt; 0 (bounded — see
    /// <see cref="SupplierFulfillmentSweepResult"/>). A failure processing one attempt is caught,
    /// recorded, and never stops the rest of the batch. This is what the background job handler calls;
    /// it needs no <c>IBackgroundJobContext</c> itself, so it's fully unit-testable on its own.
    /// </summary>
    Task<SupplierFulfillmentSweepResult> ProcessDueFulfillmentsAsync(int batchSize, CancellationToken cancellationToken = default);

    /// <summary>
    /// True once no further AUTOMATIC follow-up attempt will ever be created for this exact
    /// (order, order item, supplier, mapping) scope — i.e. the same exhaustion signal
    /// <see cref="ProcessDueFulfillmentsAsync"/>'s own sweep already uses to decide whether to create a
    /// <see cref="SupplierFulfillmentStatus.PartialFailed"/> follow-up, exposed so a caller outside this
    /// module (Commerce's fulfillment router, deciding whether a "supplier first" shortfall may now fall
    /// back to manual inventory) can observe the same conclusion without duplicating the attempt-count
    /// policy. A pure <see cref="SupplierFulfillmentStatus.Failed"/> attempt (zero delivered) is always
    /// exhausted immediately — the sweep never creates follow-ups for a total failure, only for
    /// <see cref="SupplierFulfillmentStatus.PartialFailed"/>. Returns <see langword="false"/> for any
    /// non-terminal fulfillment (nothing to conclude yet).
    /// </summary>
    Task<bool> IsScopeExhaustedAsync(
        Guid orderId, Guid orderItemId, Guid supplierId, Guid supplierProductMappingId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Everything <see cref="ISupplierFulfillmentService.RequestFulfillmentAsync"/> needs, all resolved
/// server-side by the (future) caller from authoritative data — Order/OrderItem ownership and quantity
/// bounds are the caller's responsibility (see the interface's remarks); this module never accepts
/// these values from a browser.
/// </summary>
public sealed record SupplierFulfillmentRequest(
    Guid OrderId,
    Guid OrderItemId,
    Guid SupplierId,
    Guid SupplierProductMappingId,
    int RequestedQuantity);

/// <summary>
/// A read-only snapshot of one <see cref="SupplierFulfillment"/> after an operation, including any
/// codes the provider delivered <em>in this call only</em> — this service does not persist raw codes
/// anywhere (it has no reference to Commerce's <c>OrderLicenseKey</c>, which is where they must
/// eventually be encrypted and stored). The caller is responsible for persisting
/// <see cref="DeliveredCodes"/> immediately and not retaining them longer than necessary.
/// </summary>
public sealed record SupplierFulfillmentOutcome(
    Guid FulfillmentId,
    Guid HamboxReferenceId,
    SupplierFulfillmentStatus Status,
    int RequestedQuantity,
    int DeliveredQuantity,
    int RemainingQuantity,
    IReadOnlyCollection<string>? DeliveredCodes,
    SupplierFulfillmentFailureCategory? FailureCategory);

public sealed record SupplierFulfillmentSweepResult(
    int AttemptsExamined,
    int Submitted,
    int Reconciled,
    int FollowUpsCreated,
    int ClaimRaceLosses,
    int Errors);

/// <summary>
/// Registered by whichever module owns the actual digital-goods delivery record (Commerce's
/// <c>OrderLicenseKey</c>) — invoked exactly once, at the moment delivered codes become known,
/// regardless of whether that happens synchronously (<see cref="ISupplierFulfillmentService.ProcessAsync"/>,
/// e.g. a fast/synchronous provider) or later (<see cref="ISupplierFulfillmentService.ReconcileAsync"/>,
/// the normal path for an async provider like Bamboo — its purchase call only ever confirms
/// acceptance, never delivers codes directly). This is the one generic seam that lets delivered codes
/// reach a consumer without the Suppliers module ever referencing Commerce. Optional by design: if
/// nothing is registered, delivered codes are simply not persisted anywhere beyond this one call — a
/// deliberate no-op for contexts (tests, a future consumer that doesn't need this) that never register
/// a sink, not a silent failure.
/// </summary>
public interface ISupplierFulfillmentDeliverySink
{
    /// <summary>
    /// <paramref name="supplierId"/>/<paramref name="supplierProductMappingId"/> identify which
    /// (order, order item, supplier, mapping) scope this terminal outcome belongs to.
    /// <paramref name="isScopeExhausted"/> is <see cref="ISupplierFulfillmentService.IsScopeExhaustedAsync"/>'s
    /// answer for that exact scope, computed by the caller and passed in here rather than the sink
    /// calling back into <see cref="ISupplierFulfillmentService"/> itself — <c>SupplierFulfillmentService</c>
    /// is the one production implementation of that interface and also (via DI) depends on whichever
    /// sink is registered, so a sink resolving <see cref="ISupplierFulfillmentService"/> itself would be
    /// a circular dependency. Consumers that don't need this (e.g. a future sink with no fallback
    /// concept of its own) simply ignore the parameter.
    /// </summary>
    Task OnDeliveredAsync(
        Guid orderId,
        Guid orderItemId,
        Guid supplierId,
        Guid supplierProductMappingId,
        bool isScopeExhausted,
        IReadOnlyCollection<string> deliveredCodes,
        CancellationToken cancellationToken);
}
