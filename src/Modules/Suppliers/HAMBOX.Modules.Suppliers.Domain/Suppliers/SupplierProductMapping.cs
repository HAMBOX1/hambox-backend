using HAMBOX.Domain.Entities;

namespace HAMBOX.Modules.Suppliers.Domain.Suppliers;

/// <summary>
/// Maps a HAMBOX catalog product to one supplier's external product/SKU. One internal product can
/// have several mappings (one per supplier) so future fulfillment can pick a supplier by
/// <see cref="Priority"/>. <see cref="InternalProductId"/> is a logical reference to Catalog's
/// Product — no cross-schema FK, consistent with the rest of the codebase.
/// </summary>
public sealed class SupplierProductMapping : AggregateRoot, IAuditable
{
    private SupplierProductMapping()
    {
    }

    private SupplierProductMapping(
        Guid id,
        Guid supplierId,
        Guid internalProductId,
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
        int priority)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(externalProductId);

        return new SupplierProductMapping(
            Guid.NewGuid(),
            supplierId,
            internalProductId,
            externalProductId.Trim(),
            string.IsNullOrWhiteSpace(externalSku) ? null : externalSku.Trim(),
            string.IsNullOrWhiteSpace(externalName) ? null : externalName.Trim(),
            buyingPrice,
            currency.Trim().ToUpperInvariant(),
            priority);
    }

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
