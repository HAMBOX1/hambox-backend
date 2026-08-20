using HAMBOX.Modules.Suppliers.Domain.Suppliers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HAMBOX.Modules.Suppliers.Infrastructure.Configurations;

internal sealed class SupplierConfiguration : IEntityTypeConfiguration<Supplier>
{
    public void Configure(EntityTypeBuilder<Supplier> builder)
    {
        builder.ToTable("Suppliers");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Name).HasMaxLength(200).IsRequired();
        builder.Property(s => s.Code).HasMaxLength(50).IsRequired();
        builder.Property(s => s.ProviderType).HasMaxLength(100).IsRequired();
        builder.Property(s => s.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(s => s.AuthenticationType).HasConversion<string>().HasMaxLength(20);
        builder.Property(s => s.BaseUrl).HasMaxLength(500);
        // No explicit HasColumnType: EF Core's own convention already maps an unbounded string to
        // nvarchar(max) on SQL Server — the literal "nvarchar(max)" string was provider-specific and
        // broke schema creation on any other provider (e.g. SQLite in integration tests), for no
        // behavior difference on SQL Server. Mirrors IdempotencyKeyConfiguration's identical fix.
        builder.Property(s => s.SettingsJson);
        builder.Property(s => s.Username).HasMaxLength(200);
        builder.Property(s => s.CreatedBy).HasMaxLength(128);
        builder.Property(s => s.ModifiedBy).HasMaxLength(128);

        builder.HasIndex(s => s.Code).IsUnique();
        builder.HasIndex(s => s.Priority);
        builder.HasIndex(s => s.IsDeleted);

        builder.Ignore(s => s.DomainEvents);
    }
}

internal sealed class SupplierProductMappingConfiguration : IEntityTypeConfiguration<SupplierProductMapping>
{
    public void Configure(EntityTypeBuilder<SupplierProductMapping> builder)
    {
        builder.ToTable("SupplierProductMappings");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.ExternalProductId).HasMaxLength(200).IsRequired();
        builder.Property(m => m.ExternalSku).HasMaxLength(200);
        builder.Property(m => m.ExternalName).HasMaxLength(300);
        builder.Property(m => m.Currency).HasMaxLength(3).IsRequired();
        builder.Property(m => m.BuyingPrice).HasColumnType("decimal(18,2)");
        builder.Property(m => m.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(m => m.CreatedBy).HasMaxLength(128);
        builder.Property(m => m.ModifiedBy).HasMaxLength(128);

        // Two explicit, separately-filtered unique indexes rather than one composite index over the
        // nullable InternalProductVariantId — a single composite unique index over a nullable column
        // treats every NULL as distinct (SQL Server's default), which would silently stop enforcing
        // "at most one product-wide mapping per supplier+product" the moment a second, third, etc. NULL
        // row was inserted. Splitting it out enforces both rules explicitly and independently:
        builder.HasIndex(m => new { m.SupplierId, m.InternalProductId })
            .IsUnique()
            .HasFilter("[InternalProductVariantId] IS NULL")
            .HasDatabaseName("IX_SupplierProductMappings_SupplierId_InternalProductId_ProductWide");
        builder.HasIndex(m => new { m.SupplierId, m.InternalProductVariantId })
            .IsUnique()
            .HasFilter("[InternalProductVariantId] IS NOT NULL")
            .HasDatabaseName("IX_SupplierProductMappings_SupplierId_InternalProductVariantId_Specific");
        builder.HasIndex(m => m.InternalProductId);
        builder.HasIndex(m => m.InternalProductVariantId);

        builder.Ignore(m => m.DomainEvents);
    }
}

internal sealed class SupplierAuditLogConfiguration : IEntityTypeConfiguration<SupplierAuditLog>
{
    public void Configure(EntityTypeBuilder<SupplierAuditLog> builder)
    {
        builder.ToTable("SupplierAuditLogs");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Action).HasConversion<string>().HasMaxLength(30);
        builder.Property(l => l.ActorUserId).HasMaxLength(128);
        // No explicit HasColumnType — see SupplierConfiguration.SettingsJson's comment above.
        builder.Property(l => l.DetailsJson);

        builder.HasIndex(l => new { l.SupplierId, l.CreatedOnUtc });
    }
}
