using HAMBOX.Modules.Catalog.Domain.Filters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HAMBOX.Modules.Catalog.Infrastructure.Configurations;

internal sealed class FilterDefinitionConfiguration : IEntityTypeConfiguration<FilterDefinition>
{
    public void Configure(EntityTypeBuilder<FilterDefinition> builder)
    {
        builder.ToTable("FilterDefinitions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Key).IsRequired().HasMaxLength(100);
        builder.Property(x => x.NameEn).IsRequired().HasMaxLength(200);
        builder.Property(x => x.NameAr).HasMaxLength(200);
        builder.Property(x => x.ScopeCategoryIds).HasMaxLength(4000);
        builder.Property(x => x.ModifiedBy).HasMaxLength(100);
        builder.HasIndex(x => x.Key).IsUnique();
        builder.HasMany(x => x.Values).WithOne().HasForeignKey(v => v.FilterId).OnDelete(DeleteBehavior.Cascade);
        builder.Metadata.FindNavigation(nameof(FilterDefinition.Values))!.SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class FilterValueConfiguration : IEntityTypeConfiguration<FilterValue>
{
    public void Configure(EntityTypeBuilder<FilterValue> builder)
    {
        builder.ToTable("FilterValues");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Value).IsRequired().HasMaxLength(100);
        builder.Property(x => x.LabelEn).IsRequired().HasMaxLength(200);
        builder.Property(x => x.LabelAr).HasMaxLength(200);
        builder.HasIndex(x => new { x.FilterId, x.Value }).IsUnique();
    }
}

internal sealed class FilterImportLogConfiguration : IEntityTypeConfiguration<FilterImportLog>
{
    public void Configure(EntityTypeBuilder<FilterImportLog> builder)
    {
        builder.ToTable("FilterImportLogs");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Kind).IsRequired().HasMaxLength(20);
        builder.Property(x => x.Mode).IsRequired().HasMaxLength(20);
        builder.Property(x => x.FileName).IsRequired().HasMaxLength(200);
        builder.Property(x => x.FileHash).IsRequired().HasMaxLength(64);
        builder.Property(x => x.UndoJson).IsRequired();
        builder.Property(x => x.CreatedBy).HasMaxLength(100);
        builder.HasIndex(x => new { x.Kind, x.FileHash });
        builder.HasIndex(x => x.AppliedOnUtc);
    }
}

internal sealed class ProductFilterValueConfiguration : IEntityTypeConfiguration<ProductFilterValue>
{
    public void Configure(EntityTypeBuilder<ProductFilterValue> builder)
    {
        builder.ToTable("ProductFilterValues");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.ProductId, x.FilterValueId }).IsUnique();
        builder.HasIndex(x => new { x.FilterId, x.ProductId });
        builder.HasIndex(x => x.FilterValueId);
    }
}
