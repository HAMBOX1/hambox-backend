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
        builder.Property(s => s.SettingsJson).HasColumnType("nvarchar(max)");
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

        builder.HasIndex(m => new { m.SupplierId, m.InternalProductId });
        builder.HasIndex(m => m.InternalProductId);

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
        builder.Property(l => l.DetailsJson).HasColumnType("nvarchar(max)");

        builder.HasIndex(l => new { l.SupplierId, l.CreatedOnUtc });
    }
}
