namespace HAMBOX.Modules.Commerce.Application.Abstractions;

/// <summary>
/// The one place "which supplier, at what customer-facing price" is decided. Wraps
/// <see cref="ISupplierRoutingEngine"/> (which already does all eligibility filtering — enabled,
/// credentialed, available, quantity-capable, valid <c>BuyingPrice</c> — and ranks by raw acquisition
/// cost) and re-ranks by <b>selling price</b> instead: when suppliers have different margins, the
/// cheapest-cost supplier is not necessarily the cheapest-to-customer one. This is deliberately a
/// separate interface rather than a change to <see cref="ISupplierRoutingEngine"/>'s own ranking, since
/// that engine's cost-ascending order is still the correct, independently-tested answer to "what does
/// this cost us" — margin application belongs one layer up.
/// </summary>
/// <remarks>
/// Same placement/visibility rule as <see cref="ISupplierRoutingEngine"/>: lives in
/// <c>Commerce.Application</c>, never BuildingBlocks, because it still carries supplier acquisition
/// cost. Catalog's storefront code must never depend on this — it reads the plain, cost-free price via
/// <c>IFulfillmentRouter.GetEffectivePriceOverridesBulkAsync</c> (BuildingBlocks), backed by the
/// persisted <c>SupplierDerivedPrice</c> cache this engine's result is written into.
/// </remarks>
public interface ISupplierPricingEngine
{
    Task<SupplierPricingResult> ResolveAsync(SupplierRoutingRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// One eligible candidate with its computed selling price. <see cref="SellingPrice"/> is what
/// candidates are sorted by (ascending, tie-break <c>Priority</c> then <c>SupplierId</c> — same
/// determinism rule <see cref="ISupplierRoutingEngine"/> uses).
/// </summary>
public sealed record SupplierPricingCandidate(
    Guid SupplierId,
    string SupplierName,
    string ProviderType,
    Guid SupplierProductMappingId,
    decimal CostInBaseCurrency,
    decimal SellingPrice,
    decimal MarginPercentApplied,
    decimal OriginalCost,
    string OriginalCurrency,
    int Priority);

/// <summary>
/// <see cref="RankedBySellingPriceAscending"/> is the full eligible set (not just the winner) so both
/// the storefront-price cache writer and the checkout/fulfillment failover loop can consume the same
/// ordered list. <see cref="Rejected"/> is passed through unchanged from the routing engine for admin
/// display — never exposed to a customer-facing surface.
/// </summary>
public sealed record SupplierPricingResult(
    IReadOnlyList<SupplierPricingCandidate> RankedBySellingPriceAscending,
    IReadOnlyList<SupplierRoutingRejection> Rejected,
    string BaseCurrency);
