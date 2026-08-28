using HAMBOX.Modules.Suppliers.Domain.Fulfillments;
using HAMBOX.Modules.Suppliers.Domain.Suppliers;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Suppliers.Application.Abstractions;

public interface ISuppliersDbContext
{
    DbSet<Supplier> Suppliers { get; }
    DbSet<SupplierProductMapping> SupplierProductMappings { get; }
    DbSet<SupplierAuditLog> SupplierAuditLogs { get; }
    DbSet<SupplierFulfillment> SupplierFulfillments { get; }
    DbSet<SupplierProductAvailability> SupplierProductAvailabilities { get; }
    DbSet<SupplierRoutingAuditLog> SupplierRoutingAuditLogs { get; }
    DbSet<SupplierDerivedPrice> SupplierDerivedPrices { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
