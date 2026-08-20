using HAMBOX.Modules.Suppliers.Domain.Suppliers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HAMBOX.Modules.Suppliers.Infrastructure.Configurations;

internal sealed class SupplierProductAvailabilityConfiguration : IEntityTypeConfiguration<SupplierProductAvailability>
{
    public void Configure(EntityTypeBuilder<SupplierProductAvailability> builder)
    {
        builder.ToTable("SupplierProductAvailabilities");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.ExternalProductId).IsRequired().HasMaxLength(200);
        builder.Property(a => a.AvailabilityState).HasConversion<string>().HasMaxLength(20);
        builder.Property(a => a.LastErrorMessage).HasMaxLength(500);
        builder.Property(a => a.CreatedBy).HasMaxLength(128);
        builder.Property(a => a.ModifiedBy).HasMaxLength(128);
        builder.Property(a => a.RowVersion).IsRowVersion();

        // One row per mapping — the sync service upserts by this, never creates a second row for the
        // same mapping.
        builder.HasIndex(a => a.SupplierProductMappingId).IsUnique().HasDatabaseName("IX_SupplierProductAvailabilities_SupplierProductMappingId");

        // The sync service's per-supplier update pass looks up "every row for this supplier whose
        // ExternalProductId is in this batch's resolved set" — this is that lookup's index.
        builder.HasIndex(a => new { a.SupplierId, a.ExternalProductId }).HasDatabaseName("IX_SupplierProductAvailabilities_SupplierId_ExternalProductId");

        builder.Ignore(a => a.DomainEvents);
    }
}
