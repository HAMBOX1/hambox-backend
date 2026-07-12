using HAMBOX.Domain.Entities;
using HAMBOX.Modules.Catalog.Domain.Enums;

namespace HAMBOX.Modules.Catalog.Domain.Inventory;

public sealed class DigitalInventoryCode : AggregateRoot, IAuditable
{
    private DigitalInventoryCode()
    {
    }

    private DigitalInventoryCode(
        Guid id,
        Guid variantId,
        Guid batchId,
        Guid? supplierId,
        string digitalCode,
        string? serialNumber,
        string? pin,
        decimal? purchaseCost,
        decimal? sellingPriceOverride,
        string currency,
        string? notes,
        DateTimeOffset? expirationDate)
        : base(id)
    {
        VariantId = variantId;
        BatchId = batchId;
        SupplierId = supplierId;
        DigitalCode = digitalCode;
        SerialNumber = serialNumber;
        Pin = pin;
        PurchaseCost = purchaseCost;
        SellingPriceOverride = sellingPriceOverride;
        Currency = currency;
        Notes = notes;
        ExpirationDate = expirationDate;
        Status = InventoryCodeStatus.Available;
        CodeHash = ComputeHash(digitalCode);
    }

    public Guid VariantId { get; private set; }
    public Guid BatchId { get; private set; }
    public Guid? SupplierId { get; private set; }
    public string DigitalCode { get; private set; } = string.Empty;
    public string CodeHash { get; private set; } = string.Empty;
    public string? SerialNumber { get; private set; }
    public string? Pin { get; private set; }
    public decimal? PurchaseCost { get; private set; }
    public decimal? SellingPriceOverride { get; private set; }
    public string Currency { get; private set; } = "USD";
    public string? Notes { get; private set; }
    public DateTimeOffset? ExpirationDate { get; private set; }
    public InventoryCodeStatus Status { get; private set; }
    public DateTimeOffset? ReservedOnUtc { get; private set; }
    public DateTimeOffset? SoldOnUtc { get; private set; }
    public Guid? OrderId { get; private set; }
    public Guid? OrderItemId { get; private set; }
    public string? CreatedBy { get; set; }
    public string? ModifiedBy { get; set; }

    public static DigitalInventoryCode Create(
        Guid variantId,
        Guid batchId,
        string digitalCode,
        Guid? supplierId = null,
        string? serialNumber = null,
        string? pin = null,
        decimal? purchaseCost = null,
        decimal? sellingPriceOverride = null,
        string currency = "USD",
        string? notes = null,
        DateTimeOffset? expirationDate = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(digitalCode);
        return new DigitalInventoryCode(
            Guid.NewGuid(),
            variantId,
            batchId,
            supplierId,
            digitalCode.Trim(),
            serialNumber?.Trim(),
            pin?.Trim(),
            purchaseCost,
            sellingPriceOverride,
            currency.Trim().ToUpperInvariant(),
            notes?.Trim(),
            expirationDate);
    }

    public void Reserve()
    {
        if (Status != InventoryCodeStatus.Available)
        {
            throw new InvalidOperationException("Code is not available for reservation.");
        }

        if (ExpirationDate is not null && ExpirationDate <= DateTimeOffset.UtcNow)
        {
            Status = InventoryCodeStatus.Expired;
            throw new InvalidOperationException("Code has expired.");
        }

        Status = InventoryCodeStatus.Reserved;
        ReservedOnUtc = DateTimeOffset.UtcNow;
    }

    public void Release()
    {
        if (Status != InventoryCodeStatus.Reserved)
        {
            throw new InvalidOperationException("Code is not reserved.");
        }

        Status = InventoryCodeStatus.Available;
        ReservedOnUtc = null;
    }

    public void MarkSold(Guid orderId, Guid orderItemId)
    {
        if (Status != InventoryCodeStatus.Reserved)
        {
            throw new InvalidOperationException("Code must be reserved before sale.");
        }

        Status = InventoryCodeStatus.Sold;
        SoldOnUtc = DateTimeOffset.UtcNow;
        OrderId = orderId;
        OrderItemId = orderItemId;
    }

    public void Disable()
    {
        Status = InventoryCodeStatus.Disabled;
    }

    public void Enable()
    {
        if (Status == InventoryCodeStatus.Disabled)
        {
            Status = InventoryCodeStatus.Available;
        }
    }

    public void MoveToBatch(Guid batchId) => BatchId = batchId;

    public static string ComputeHash(string code) =>
        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(code.Trim().ToUpperInvariant())));
}
