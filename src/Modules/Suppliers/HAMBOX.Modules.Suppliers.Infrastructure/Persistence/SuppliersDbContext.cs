using HAMBOX.Application.Abstractions;
using HAMBOX.Domain.Entities;
using HAMBOX.Infrastructure.Persistence.Conversions;
using HAMBOX.Modules.Suppliers.Application.Abstractions;
using HAMBOX.Modules.Suppliers.Domain.Fulfillments;
using HAMBOX.Modules.Suppliers.Domain.Suppliers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace HAMBOX.Modules.Suppliers.Infrastructure.Persistence;

public sealed class SuppliersDbContext(DbContextOptions<SuppliersDbContext> options, ICodeProtector codeProtector)
    : DbContext(options), ISuppliersDbContext
{
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<SupplierProductMapping> SupplierProductMappings => Set<SupplierProductMapping>();
    public DbSet<SupplierAuditLog> SupplierAuditLogs => Set<SupplierAuditLog>();
    public DbSet<SupplierFulfillment> SupplierFulfillments => Set<SupplierFulfillment>();
    public DbSet<SupplierProductAvailability> SupplierProductAvailabilities => Set<SupplierProductAvailability>();
    public DbSet<SupplierRoutingAuditLog> SupplierRoutingAuditLogs => Set<SupplierRoutingAuditLog>();
    public DbSet<SupplierDerivedPrice> SupplierDerivedPrices => Set<SupplierDerivedPrice>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasDefaultSchema("suppliers");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SuppliersDbContext).Assembly);

        ApplyCredentialEncryption(modelBuilder);
        ApplyGlobalQueryFilters(modelBuilder);
    }

    /// <summary>
    /// Encrypts supplier credential secrets at rest, mirroring the digital-inventory-code encryption
    /// pattern in Catalog. Storage is ciphertext only; handlers keep reading/writing plaintext.
    /// </summary>
    private void ApplyCredentialEncryption(ModelBuilder modelBuilder)
    {
        ValueConverter converter = new EncryptedStringConverter(codeProtector);

        var supplier = modelBuilder.Entity<Supplier>();
        supplier.Property(s => s.ApiKey).HasConversion(converter).HasMaxLength(2000);
        supplier.Property(s => s.ApiSecret).HasConversion(converter).HasMaxLength(2000);
        supplier.Property(s => s.Password).HasConversion(converter).HasMaxLength(2000);
        supplier.Property(s => s.BearerToken).HasConversion(converter).HasMaxLength(2000);
        // No explicit HasColumnType here either — see SupplierConfiguration.SettingsJson's comment.
        supplier.Property(s => s.OAuthSettingsJson).HasConversion(converter);
    }

    private static void ApplyGlobalQueryFilters(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(ISoftDeletable).IsAssignableFrom(entityType.ClrType))
            {
                continue;
            }

            var parameter = System.Linq.Expressions.Expression.Parameter(entityType.ClrType, "e");
            var property = System.Linq.Expressions.Expression.Property(parameter, nameof(ISoftDeletable.IsDeleted));
            var condition = System.Linq.Expressions.Expression.Equal(
                property,
                System.Linq.Expressions.Expression.Constant(false));
            var lambda = System.Linq.Expressions.Expression.Lambda(condition, parameter);

            modelBuilder.Entity(entityType.ClrType).HasQueryFilter(lambda);
        }
    }
}
