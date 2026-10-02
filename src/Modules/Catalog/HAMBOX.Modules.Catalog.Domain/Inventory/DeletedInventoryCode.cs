using HAMBOX.Domain.Entities;
using HAMBOX.Modules.Catalog.Domain.Enums;

namespace HAMBOX.Modules.Catalog.Domain.Inventory;

/// <summary>
/// Permanent snapshot of a <see cref="DigitalInventoryCode"/> taken the moment it is deleted. Deleting a
/// code removes it from live stock, but the value itself must never be lost — it stays here and is
/// readable only by the primary admin (Owner). Never read by storefront, checkout or stock counting.
/// </summary>
public sealed class DeletedInventoryCode : AggregateRoot
{
    private DeletedInventoryCode()
    {
    }

    private DeletedInventoryCode(Guid id, DigitalInventoryCode code, Guid? deletedByUserId, string reason)
        : base(id)
    {
        OriginalCodeId = code.Id;
        VariantId = code.VariantId;
        BatchId = code.BatchId;
        SupplierId = code.SupplierId;
        DigitalCode = code.DigitalCode;
        SerialNumber = code.SerialNumber;
        Pin = code.Pin;
        PurchaseCost = code.PurchaseCost;
        Currency = code.Currency;
        Notes = code.Notes;
        ExpirationDate = code.ExpirationDate;
        StatusAtDeletion = code.Status;
        DeletedOnUtc = DateTimeOffset.UtcNow;
        DeletedByUserId = deletedByUserId;
        Reason = reason;
    }

    public Guid OriginalCodeId { get; private set; }
    public Guid VariantId { get; private set; }
    public Guid BatchId { get; private set; }
    public Guid? SupplierId { get; private set; }
    public string DigitalCode { get; private set; } = string.Empty;
    public string? SerialNumber { get; private set; }
    public string? Pin { get; private set; }
    public decimal? PurchaseCost { get; private set; }
    public string Currency { get; private set; } = "USD";
    public string? Notes { get; private set; }
    public DateTimeOffset? ExpirationDate { get; private set; }
    public InventoryCodeStatus StatusAtDeletion { get; private set; }
    public DateTimeOffset DeletedOnUtc { get; private set; }
    public Guid? DeletedByUserId { get; private set; }
    public string Reason { get; private set; } = string.Empty;

    public static DeletedInventoryCode Archive(DigitalInventoryCode code, string? deletedByUserId, string reason) =>
        new(Guid.NewGuid(), code, Guid.TryParse(deletedByUserId, out var userId) ? userId : null, reason);
}
