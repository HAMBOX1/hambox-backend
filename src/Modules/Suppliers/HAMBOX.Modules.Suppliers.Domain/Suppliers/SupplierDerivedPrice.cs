using HAMBOX.Domain.Entities;

namespace HAMBOX.Modules.Suppliers.Domain.Suppliers;

/// <summary>
/// Persisted, never-live cache of the customer-facing price derived from the cheapest eligible
/// supplier's acquisition cost plus margin, for one <c>SupplierFirst</c>/<c>SupplierOnly</c> variant.
/// Mirrors <see cref="SupplierProductAvailability"/>'s "computed by a background sync, read directly by
/// the storefront path, never recomputed live per-request" discipline — <c>IFulfillmentRouter</c>
/// (BuildingBlocks) reads this table directly (bulk, indexed) so Catalog's storefront queries never
/// depend on Commerce/Suppliers or trigger a live supplier call.
/// </summary>
/// <remarks>
/// One row per <see cref="InternalProductVariantId"/> — a variant with no eligible supplier candidate
/// has no row at all (see <c>ISupplierPricingEngine</c>'s caller), so storefront/checkout code falls
/// back to <c>PriceOverride ?? Product.Price</c> simply by finding nothing here. A row is only ever
/// overwritten by a successful recompute; a failed or empty recompute leaves the previous row exactly
/// as-is, so a transient supplier outage never flickers or zeroes the storefront price.
/// </remarks>
public sealed class SupplierDerivedPrice : AggregateRoot, IAuditable
{
    private SupplierDerivedPrice()
    {
    }

    private SupplierDerivedPrice(
        Guid id,
        Guid internalProductId,
        Guid internalProductVariantId,
        decimal effectivePrice,
        Guid selectedSupplierId,
        Guid selectedSupplierProductMappingId,
        decimal appliedMarginPercent,
        string baseCurrency)
        : base(id)
    {
        InternalProductId = internalProductId;
        InternalProductVariantId = internalProductVariantId;
        EffectivePrice = effectivePrice;
        SelectedSupplierId = selectedSupplierId;
        SelectedSupplierProductMappingId = selectedSupplierProductMappingId;
        AppliedMarginPercent = appliedMarginPercent;
        BaseCurrency = baseCurrency;
        ComputedOnUtc = DateTimeOffset.UtcNow;
    }

    public Guid InternalProductId { get; private set; }
    public Guid InternalProductVariantId { get; private set; }
    public decimal EffectivePrice { get; private set; }
    public Guid SelectedSupplierId { get; private set; }
    public Guid SelectedSupplierProductMappingId { get; private set; }
    public decimal AppliedMarginPercent { get; private set; }
    public string BaseCurrency { get; private set; } = "USD";
    public DateTimeOffset ComputedOnUtc { get; private set; }
    public string? CreatedBy { get; set; }
    public string? ModifiedBy { get; set; }

    public static SupplierDerivedPrice Create(
        Guid internalProductId,
        Guid internalProductVariantId,
        decimal effectivePrice,
        Guid selectedSupplierId,
        Guid selectedSupplierProductMappingId,
        decimal appliedMarginPercent,
        string baseCurrency) => new(
        Guid.NewGuid(),
        internalProductId,
        internalProductVariantId,
        effectivePrice,
        selectedSupplierId,
        selectedSupplierProductMappingId,
        appliedMarginPercent,
        baseCurrency);

    /// <summary>Overwrites this row with a fresh successful recompute — only ever called with a real winning candidate.</summary>
    public void Recompute(
        decimal effectivePrice,
        Guid selectedSupplierId,
        Guid selectedSupplierProductMappingId,
        decimal appliedMarginPercent,
        string baseCurrency)
    {
        EffectivePrice = effectivePrice;
        SelectedSupplierId = selectedSupplierId;
        SelectedSupplierProductMappingId = selectedSupplierProductMappingId;
        AppliedMarginPercent = appliedMarginPercent;
        BaseCurrency = baseCurrency;
        ComputedOnUtc = DateTimeOffset.UtcNow;
    }
}
