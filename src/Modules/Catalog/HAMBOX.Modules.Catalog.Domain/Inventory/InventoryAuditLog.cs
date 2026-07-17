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
        string? details,
        Guid? orderId,
        string? ipAddress,
        string? userAgent)
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
        OrderId = orderId;
        IpAddress = ipAddress;
        UserAgent = userAgent;
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

    /// <summary>Order the code was sold on, if known at the time of the action (e.g. a reveal on a delivered code).</summary>
    public Guid? OrderId { get; private set; }
    public string? IpAddress { get; private set; }
    public string? UserAgent { get; private set; }

    public static InventoryAuditLog Create(
        InventoryAuditAction action,
        Guid? productId = null,
        Guid? variantId = null,
        Guid? batchId = null,
        Guid? codeId = null,
        Guid? supplierId = null,
        string? performedByUserId = null,
        string? details = null,
        Guid? orderId = null,
        string? ipAddress = null,
        string? userAgent = null) =>
        new(Guid.NewGuid(), action, productId, variantId, batchId, codeId, supplierId, performedByUserId, details, orderId, ipAddress, userAgent);
}
