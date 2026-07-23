using HAMBOX.Modules.Catalog.Domain.Packaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HAMBOX.Modules.Catalog.Infrastructure.Configurations;

internal sealed class CatalogPackageJobConfiguration : IEntityTypeConfiguration<CatalogPackageJob>
{
    public void Configure(EntityTypeBuilder<CatalogPackageJob> builder)
    {
        builder.ToTable("CatalogPackageJobs");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Direction).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Format).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.EntityType).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.StorageKey).IsRequired().HasMaxLength(500);
        builder.Property(x => x.FileName).IsRequired().HasMaxLength(300);
        builder.Property(x => x.ResultStorageKey).HasMaxLength(500);
        builder.Property(x => x.ResultFileName).HasMaxLength(300);
        builder.Property(x => x.SummaryJson).HasColumnType("nvarchar(max)");
        builder.Property(x => x.ErrorMessage).HasMaxLength(2000);
        builder.HasIndex(x => x.CreatedOnUtc);
        builder.HasIndex(x => x.BackgroundJobId);
    }
}
