namespace HAMBOX.Modules.Commerce.Application.Abstractions;

/// <summary>
/// The one place "which supplier should HAMBOX buy this from" is decided — cheapest eligible acquisition
/// cost wins, entirely server-side, entirely from data already persisted (mappings, the availability
/// cache, provider-declared capabilities). Deliberately lives in <c>Commerce.Application</c>, NOT
/// BuildingBlocks like <see cref="IFulfillmentRouter"/> — unlike that interface (which Catalog's
/// storefront code also depends on for a "is this purchasable" yes/no), this one carries supplier
/// acquisition cost and must never be reachable from Catalog or any customer-facing surface.
/// </summary>
/// <remarks>
/// Never calls a provider live (no HTTP) — same "fast local decision" constraint
/// <see cref="IFulfillmentRouter"/> already has, for the same reason (checkout/fulfillment must not be
/// slowed down by a live per-supplier connection test). The actual purchase attempt is what finally
/// proves a candidate really works; this only ranks candidates by what's already known.
/// </remarks>
public interface ISupplierRoutingEngine
{
    Task<SupplierRoutingResult> ResolveAsync(SupplierRoutingRequest request, CancellationToken cancellationToken = default);
}

public sealed record SupplierRoutingRequest(Guid ProductId, Guid VariantId, int Quantity);

/// <summary>
/// One eligible candidate, ranked. <see cref="CostInBaseCurrency"/> is what candidates are actually
/// sorted by; <see cref="OriginalCost"/>/<see cref="OriginalCurrency"/> are kept alongside purely for
/// admin-audit display (never for comparison — comparing raw numbers across currencies would be wrong).
/// </summary>
public sealed record SupplierRoutingCandidate(
    Guid SupplierId,
    string SupplierName,
    string ProviderType,
    Guid SupplierProductMappingId,
    decimal CostInBaseCurrency,
    decimal OriginalCost,
    string OriginalCurrency,
    int Priority,
    /// <summary>Null means "use the platform default margin" — see <c>ISupplierPricingEngine</c>, the only consumer of this field.</summary>
    decimal? MarginPercentOverride = null);

/// <summary>A mapping considered and excluded — <see cref="Reason"/> is safe, admin-facing text only (never a credential, never a raw provider response).</summary>
public sealed record SupplierRoutingRejection(Guid SupplierId, string SupplierName, Guid? SupplierProductMappingId, string Reason);

/// <summary>
/// <see cref="EligibleByCostAscending"/> is already fully sorted (cost asc, then <c>Priority</c> asc,
/// then <c>SupplierId</c> — deterministic, never random) — the caller just walks it in order for
/// failover. Both lists exist purely to be persisted for admin visibility; neither is ever exposed to a
/// customer-facing surface.
/// </summary>
public sealed record SupplierRoutingResult(
    IReadOnlyList<SupplierRoutingCandidate> EligibleByCostAscending,
    IReadOnlyList<SupplierRoutingRejection> Rejected,
    string BaseCurrency);
