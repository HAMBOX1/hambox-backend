using HAMBOX.Domain.Entities;
using HAMBOX.Modules.Catalog.Domain.Enums;

namespace HAMBOX.Modules.Catalog.Domain.Inventory;

public sealed class InventoryBatch : AggregateRoot, IAuditable
{
    private InventoryBatch()
    {
    }

    private InventoryBatch(
        Guid id,
        Guid variantId,
        Guid? supplierId,
        string name,
        DateTimeOffset? purchaseDate,
        string currency,
        decimal purchaseCost,
        decimal? expectedMargin,
        string? notes)
        : base(id)
    {
        VariantId = variantId;
        SupplierId = supplierId;
        Name = name;
        PurchaseDate = purchaseDate;
        ImportDate = DateTimeOffset.UtcNow;
        Currency = currency;
        PurchaseCost = purchaseCost;
        ExpectedMargin = expectedMargin;
        Notes = notes;
    }

    public Guid VariantId { get; private set; }
    public Guid? SupplierId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public DateTimeOffset? PurchaseDate { get; private set; }
    public DateTimeOffset ImportDate { get; private set; }
    public string Currency { get; private set; } = "USD";
    public decimal PurchaseCost { get; private set; }
    public decimal? ExpectedMargin { get; private set; }
    public string? Notes { get; private set; }
    public int TotalCodes { get; private set; }
    public int AvailableCodes { get; private set; }
    public int ReservedCodes { get; private set; }
    public int SoldCodes { get; private set; }
    public int ReturnedCodes { get; private set; }
    public int ExpiredCodes { get; private set; }
    public string? CreatedBy { get; set; }
    public string? ModifiedBy { get; set; }

    public static InventoryBatch Create(
        Guid variantId,
        string name,
        Guid? supplierId = null,
        DateTimeOffset? purchaseDate = null,
        string currency = "USD",
        decimal purchaseCost = 0,
        decimal? expectedMargin = null,
        string? notes = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return new InventoryBatch(
            Guid.NewGuid(),
            variantId,
            supplierId,
            name.Trim(),
            purchaseDate,
            currency.Trim().ToUpperInvariant(),
            purchaseCost,
            expectedMargin,
            notes?.Trim());
    }

    public void RecordImport(int count)
    {
        TotalCodes += count;
        AvailableCodes += count;
    }

    public void OnCodeStatusChanged(InventoryCodeStatus from, InventoryCodeStatus to)
    {
        AdjustCounter(from, -1);
        AdjustCounter(to, 1);
    }

    private void AdjustCounter(InventoryCodeStatus status, int delta)
    {
        switch (status)
        {
            case InventoryCodeStatus.Available:
                AvailableCodes += delta;
                break;
            case InventoryCodeStatus.Reserved:
                ReservedCodes += delta;
                break;
            case InventoryCodeStatus.Sold:
                SoldCodes += delta;
                break;
            case InventoryCodeStatus.Returned:
                ReturnedCodes += delta;
                break;
            case InventoryCodeStatus.Expired:
                ExpiredCodes += delta;
                break;
        }
    }
}
