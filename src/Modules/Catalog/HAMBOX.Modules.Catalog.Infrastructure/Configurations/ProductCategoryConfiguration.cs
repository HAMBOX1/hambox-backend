using HAMBOX.Modules.Catalog.Domain.Categories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HAMBOX.Modules.Catalog.Infrastructure.Configurations;

/// <summary>
/// Configures the <see cref="ProductCategory"/> entity for Entity Framework Core.
/// </summary>
internal sealed class ProductCategoryConfiguration : IEntityTypeConfiguration<ProductCategory>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ProductCategory> builder)
    {
        builder.ToTable("ProductCategories");

        // Primary key
        builder.HasKey(pc => pc.Id);

        // Properties
        builder.Property(pc => pc.ProductId)
            .IsRequired();

        builder.Property(pc => pc.CategoryId)
            .IsRequired();

        // Base entity properties
        builder.Property(pc => pc.CreatedOnUtc)
            .IsRequired();

        builder.Property(pc => pc.ModifiedOnUtc);

        // Relationships (the Product -> AdditionalCategories side is configured in ProductConfiguration)
        builder.HasOne<Category>()
            .WithMany()
            .HasForeignKey(pc => pc.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        // Indexes
        builder.HasIndex(pc => new { pc.ProductId, pc.CategoryId })
            .IsUnique()
            .HasDatabaseName("IX_ProductCategories_ProductId_CategoryId");

        builder.HasIndex(pc => pc.CategoryId)
            .HasDatabaseName("IX_ProductCategories_CategoryId");
    }
}
