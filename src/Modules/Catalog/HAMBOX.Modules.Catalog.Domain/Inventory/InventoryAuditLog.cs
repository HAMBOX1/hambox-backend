using HAMBOX.Domain.Entities;
using HAMBOX.Modules.Catalog.Domain.Enums;

namespace HAMBOX.Modules.Catalog.Domain.Inventory;

public sealed class InventoryAuditLog : Entity
{
    private InventoryAuditLog()
    {
    }

    private InventoryAuditLog(
        Guid id,
        InventoryAuditAction action,
        Guid? productId,
        Guid? variantId,
        Guid? batchId,
        Guid? codeId,
        Guid? supplierId,
        string? performedByUserId,
        string? details)
        : base(id)
    {
        Action = action;
        ProductId = productId;
        VariantId = variantId;
        BatchId = batchId;
        CodeId = codeId;
        SupplierId = supplierId;
        PerformedByUserId = performedByUserId;
        Details = details;
        OccurredOnUtc = DateTimeOffset.UtcNow;
    }

    public InventoryAuditAction Action { get; private set; }
    public Guid? ProductId { get; private set; }
    public Guid? VariantId { get; private set; }
    public Guid? BatchId { get; private set; }
    public Guid? CodeId { get; private set; }
    public Guid? SupplierId { get; private set; }
    public string? PerformedByUserId { get; private set; }
    public string? Details { get; private set; }
    public DateTimeOffset OccurredOnUtc { get; private set; }

    public static InventoryAuditLog Create(
        InventoryAuditAction action,
        Guid? productId = null,
        Guid? variantId = null,
        Guid? batchId = null,
        Guid? codeId = null,
        Guid? supplierId = null,
        string? performedByUserId = null,
        string? details = null) =>
        new(Guid.NewGuid(), action, productId, variantId, batchId, codeId, supplierId, performedByUserId, details);
}
