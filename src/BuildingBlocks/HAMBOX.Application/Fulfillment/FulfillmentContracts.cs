using HAMBOX.Modules.Catalog.Domain.Enums;

namespace HAMBOX.Application.Fulfillment;

/// <summary>
/// <see cref="SupplierId"/>/<see cref="SupplierProductMappingId"/> of the single best-priority, fully
/// READY automated-supplier match for a variant (variant-specific mapping preferred over product-wide,
/// then by <c>SupplierProductMapping.Priority</c>) — never a not-ready one; a not-ready candidate is
/// simply skipped during resolution, never surfaced.
/// </summary>
public sealed record FulfillmentSupplierCandidate(Guid SupplierId, Guid SupplierProductMappingId);

/// <summary>
/// Answer to "given this variant's configured <see cref="FulfillmentMode"/>, is manual inventory
/// allowed, and what is the best-priority READY automated-supplier candidate, if any."
/// </summary>
public sealed record FulfillmentReadiness(FulfillmentMode Mode, bool ManualAllowed, FulfillmentSupplierCandidate? SupplierCandidate)
{
    public bool SupplierReady => SupplierCandidate is not null;
}

/// <summary>
/// The one contract every module depends on to ask "is this variant's automated-supplier fulfillment
/// route ready" — the single source of truth for supplier readiness, consulted identically at
/// checkout time (<c>CartLineValidator</c>) and storefront-display time (Catalog's storefront
/// configuration queries). Implemented in Commerce.Application (<c>FulfillmentRouter</c>), registered
/// in DI. Pure read query, no side effects, no external HTTP calls — this contract lives in
/// BuildingBlocks (rather than Commerce.Application, which Catalog.Application cannot reference
/// without inverting the existing Commerce→Catalog module dependency) so Catalog can consult supplier
/// readiness without ever depending on the Commerce or Suppliers modules directly, and without ever
/// talking to a supplier provider itself.
/// </summary>
public interface IFulfillmentRouter
{
    Task<FulfillmentReadiness> GetReadinessAsync(Guid variantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Bulk form for callers resolving many variants in one page/response (storefront product list,
    /// PDP configuration) — avoids one round trip per variant. Returns the exact same per-variant
    /// answer <see cref="GetReadinessAsync"/> would for each id; a variant id with no entry in the
    /// result was not found (treat the same way <see cref="GetReadinessAsync"/> treats an unknown id).
    /// </summary>
    Task<IReadOnlyDictionary<Guid, FulfillmentReadiness>> GetReadinessBulkAsync(
        IEnumerable<Guid> variantIds, CancellationToken cancellationToken = default);
}

/// <summary>
/// The single, shared purchasability decision for a variant given its fulfillment configuration —
/// used identically by checkout (<c>CartLineValidator</c>) and storefront display (Catalog's
/// storefront configuration builder) so the two surfaces can never disagree. Mirrors
/// <c>CartLineValidator</c>'s original inline switch exactly; do not add new modes or branches here
/// without updating both callers' understanding of what "purchasable" means.
/// </summary>
public static class FulfillmentAvailability
{
    public static bool IsAvailable(FulfillmentMode mode, bool manualSufficient, bool supplierReady) => mode switch
    {
        FulfillmentMode.ManualOnly => manualSufficient,
        FulfillmentMode.ManualFirst => manualSufficient || supplierReady,
        FulfillmentMode.SupplierFirst or FulfillmentMode.SupplierOnly => supplierReady,
        _ => false,
    };
}
