using HAMBOX.Modules.Suppliers.Application.Abstractions;
using HAMBOX.Modules.Suppliers.Domain.Suppliers;

namespace HAMBOX.Modules.Suppliers.Application.Services;

public static class SupplierAuditWriter
{
    public static void Record(
        ISuppliersDbContext dbContext,
        Guid supplierId,
        SupplierAuditAction action,
        string? actorUserId,
        string? detailsJson = null)
    {
        dbContext.SupplierAuditLogs.Add(SupplierAuditLog.Create(supplierId, action, actorUserId, detailsJson));
    }
}
