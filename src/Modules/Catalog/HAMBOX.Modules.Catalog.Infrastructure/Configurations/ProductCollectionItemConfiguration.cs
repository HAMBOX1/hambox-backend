using HAMBOX.Modules.Catalog.Domain.Collections;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HAMBOX.Modules.Catalog.Infrastructure.Configurations;

/// <summary>
/// Configures the <see cref="ProductCollectionItem"/> entity for Entity Framework Core.
/// </summary>
internal sealed class ProductCollectionItemConfiguration : IEntityTypeConfiguration<ProductCollectionItem>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ProductCollectionItem> builder)
    {
        builder.ToTable("ProductCollectionItems");

        // Primary key
        builder.HasKey(pc => pc.Id);

        // Properties
        builder.Property(pc => pc.ProductId)
            .IsRequired();

        builder.Property(pc => pc.CollectionId)
            .IsRequired();

        // Base entity properties
        builder.Property(pc => pc.CreatedOnUtc)
            .IsRequired();

        builder.Property(pc => pc.ModifiedOnUtc);

        // Relationships (the Product -> Collections side is configured in ProductConfiguration)
        builder.HasOne<ProductCollection>()
            .WithMany()
            .HasForeignKey(pc => pc.CollectionId)
            .OnDelete(DeleteBehavior.Restrict);

        // Indexes
        builder.HasIndex(pc => new { pc.ProductId, pc.CollectionId })
            .IsUnique()
            .HasDatabaseName("IX_ProductCollectionItems_ProductId_CollectionId");

        builder.HasIndex(pc => pc.CollectionId)
            .HasDatabaseName("IX_ProductCollectionItems_CollectionId");
    }
}
