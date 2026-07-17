using HAMBOX.Domain.Entities;

namespace HAMBOX.Modules.Suppliers.Domain.Suppliers;

public sealed class SupplierAuditLog : BaseEntity
{
    private SupplierAuditLog()
    {
    }

    private SupplierAuditLog(Guid id, Guid supplierId, SupplierAuditAction action, string? actorUserId, string? detailsJson)
        : base(id)
    {
        SupplierId = supplierId;
        Action = action;
        ActorUserId = actorUserId;
        DetailsJson = detailsJson;
    }

    public Guid SupplierId { get; private set; }
    public SupplierAuditAction Action { get; private set; }
    public string? ActorUserId { get; private set; }
    public string? DetailsJson { get; private set; }

    public static SupplierAuditLog Create(Guid supplierId, SupplierAuditAction action, string? actorUserId, string? detailsJson = null) =>
        new(Guid.NewGuid(), supplierId, action, actorUserId, detailsJson);
}
