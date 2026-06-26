using HAMBOX.Modules.Catalog.Domain.Images;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HAMBOX.Modules.Catalog.Infrastructure.Configurations;

/// <summary>
/// Configures the <see cref="ProductImage"/> entity for Entity Framework Core.
/// </summary>
internal sealed class ProductImageConfiguration : IEntityTypeConfiguration<ProductImage>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ProductImage> builder)
    {
        builder.ToTable("ProductImages");

        // Primary key
        builder.HasKey(i => i.Id);

        // Properties
        builder.Property(i => i.ProductId)
            .IsRequired();

        builder.Property(i => i.Url)
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(i => i.StorageKey)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(i => i.FileName)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(i => i.ContentType)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(i => i.FileSizeBytes)
            .IsRequired();

        builder.Property(i => i.DisplayOrder)
            .IsRequired();

        builder.Property(i => i.IsPrimary)
            .IsRequired();

        // Base entity properties
        builder.Property(i => i.CreatedOnUtc)
            .IsRequired();

        builder.Property(i => i.ModifiedOnUtc);

        // Indexes
        builder.HasIndex(i => i.ProductId)
            .HasDatabaseName("IX_ProductImages_ProductId");
    }
}
