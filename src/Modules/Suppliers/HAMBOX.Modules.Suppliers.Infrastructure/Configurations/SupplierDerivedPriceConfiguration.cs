using HAMBOX.Modules.Suppliers.Domain.Suppliers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HAMBOX.Modules.Suppliers.Infrastructure.Configurations;

internal sealed class SupplierDerivedPriceConfiguration : IEntityTypeConfiguration<SupplierDerivedPrice>
{
    public void Configure(EntityTypeBuilder<SupplierDerivedPrice> builder)
    {
        builder.ToTable("SupplierDerivedPrices");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.EffectivePrice).HasColumnType("decimal(18,2)");
        builder.Property(p => p.AppliedMarginPercent).HasColumnType("decimal(8,2)");
        builder.Property(p => p.BaseCurrency).HasMaxLength(3).IsRequired();
        builder.Property(p => p.CreatedBy).HasMaxLength(128);
        builder.Property(p => p.ModifiedBy).HasMaxLength(128);

        // One row per variant — recompute upserts by this; a variant with no eligible supplier has no row.
        builder.HasIndex(p => p.InternalProductVariantId).IsUnique().HasDatabaseName("IX_SupplierDerivedPrices_InternalProductVariantId");

        // Storefront list/search reads "cheapest across this product's variants" in one bulk query.
        builder.HasIndex(p => p.InternalProductId).HasDatabaseName("IX_SupplierDerivedPrices_InternalProductId");

        builder.Ignore(p => p.DomainEvents);
    }
}
