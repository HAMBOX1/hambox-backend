using HAMBOX.Domain.Entities;

namespace HAMBOX.Modules.Suppliers.Domain.Suppliers;

/// <summary>
/// Maps a HAMBOX catalog product (optionally one specific variant of it) to one supplier's external
/// product/SKU. One internal product can have several mappings (one per supplier, and optionally one
/// per variant within a supplier) so fulfillment routing can pick a supplier by <see cref="Priority"/>.
/// <see cref="InternalProductId"/>/<see cref="InternalProductVariantId"/> are logical references to
/// Catalog's Product/ProductVariant — no cross-schema FK, consistent with the rest of the codebase.
/// </summary>
/// <remarks>
/// <see cref="InternalProductVariantId"/> is <see langword="null"/> for a product-wide mapping (applies
/// to every variant of the product that has no more specific mapping of its own — this was the only
/// shape that existed before it was added) or set for a mapping that applies to exactly one variant
/// (needed when different variants of one product — e.g. different face values — must purchase
/// different external products/prices from the same supplier). Resolution always prefers an exact
/// variant match over a product-wide one; see <c>OrderFulfillmentService</c>'s supplier-chain
/// resolution for the precedence rule and its regression test.
/// </remarks>
public sealed class SupplierProductMapping : AggregateRoot, IAuditable
{
    private SupplierProductMapping()
    {
    }

    private SupplierProductMapping(
        Guid id,
        Guid supplierId,
        Guid internalProductId,
        Guid? internalProductVariantId,
        string externalProductId,
        string? externalSku,
        string? externalName,
        decimal buyingPrice,
        string currency,
        int priority)
        : base(id)
    {
        SupplierId = supplierId;
        InternalProductId = internalProductId;
        InternalProductVariantId = internalProductVariantId;
        ExternalProductId = externalProductId;
        ExternalSku = externalSku;
        ExternalName = externalName;
        BuyingPrice = buyingPrice;
        Currency = currency;
        Priority = priority;
        Status = SupplierMappingStatus.Active;
    }

    public Guid SupplierId { get; private set; }
    public Guid InternalProductId { get; private set; }
    public Guid? InternalProductVariantId { get; private set; }
    public string ExternalProductId { get; private set; } = string.Empty;
    public string? ExternalSku { get; private set; }
    public string? ExternalName { get; private set; }
    public decimal BuyingPrice { get; private set; }
    public string Currency { get; private set; } = "USD";
    public int Priority { get; private set; }
    public SupplierMappingStatus Status { get; private set; }
    public string? CreatedBy { get; set; }
    public string? ModifiedBy { get; set; }

    public static SupplierProductMapping Create(
        Guid supplierId,
        Guid internalProductId,
        string externalProductId,
        string? externalSku,
        string? externalName,
        decimal buyingPrice,
        string currency,
        int priority,
        Guid? internalProductVariantId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(externalProductId);

        return new SupplierProductMapping(
            Guid.NewGuid(),
            supplierId,
            internalProductId,
            internalProductVariantId,
            externalProductId.Trim(),
            string.IsNullOrWhiteSpace(externalSku) ? null : externalSku.Trim(),
            string.IsNullOrWhiteSpace(externalName) ? null : externalName.Trim(),
            buyingPrice,
            currency.Trim().ToUpperInvariant(),
            priority);
    }

    /// <summary>
    /// Priority-only update, kept separate from <see cref="Update"/> so the admin UI's chain-reorder
    /// action (dragging mappings that may belong to different suppliers into a new order) never needs
    /// to round-trip every other field just to persist a new position — mirrors <c>Supplier.UpdatePriority</c>'s
    /// identical narrow-update pattern.
    /// </summary>
    public void UpdatePriority(int priority) => Priority = priority;

    public void Update(
        string externalProductId,
        string? externalSku,
        string? externalName,
        decimal buyingPrice,
        string currency,
        int priority,
        SupplierMappingStatus status)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(externalProductId);

        ExternalProductId = externalProductId.Trim();
        ExternalSku = string.IsNullOrWhiteSpace(externalSku) ? null : externalSku.Trim();
        ExternalName = string.IsNullOrWhiteSpace(externalName) ? null : externalName.Trim();
        BuyingPrice = buyingPrice;
        Currency = currency.Trim().ToUpperInvariant();
        Priority = priority;
        Status = status;
    }
}
