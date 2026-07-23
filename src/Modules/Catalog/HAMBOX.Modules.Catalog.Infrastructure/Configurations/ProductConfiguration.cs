using HAMBOX.Modules.Catalog.Domain.Products;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HAMBOX.Modules.Catalog.Infrastructure.Configurations;

/// <summary>
/// Configures the <see cref="Product"/> entity for Entity Framework Core.
/// </summary>
internal sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("Products");

        // Primary key
        builder.HasKey(p => p.Id);

        // Properties
        builder.Property(p => p.NameAr)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(p => p.NameEn)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(p => p.DescriptionAr)
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(p => p.DescriptionEn)
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(p => p.Price)
            .IsRequired()
            .HasColumnType("decimal(18,2)");

        builder.Property(p => p.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(p => p.CategoryId)
            .IsRequired();

        builder.Property(p => p.StockQuantity)
            .IsRequired()
            .HasDefaultValue(100);

        builder.Property(p => p.ReservedQuantity)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Ignore(p => p.AvailableStock);

        // Audit properties (IAuditable)
        builder.Property(p => p.CreatedBy)
            .HasMaxLength(256);

        builder.Property(p => p.ModifiedBy)
            .HasMaxLength(256);

        // Soft delete properties (ISoftDeletable)
        builder.Property(p => p.IsDeleted)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(p => p.DeletedOnUtc);

        // Base entity properties
        builder.Property(p => p.CreatedOnUtc)
            .IsRequired();

        builder.Property(p => p.ModifiedOnUtc);

        // Concurrency
        builder.Property<byte[]>("RowVersion")
            .IsRowVersion();

        // Relationships
        builder.HasOne<HAMBOX.Modules.Catalog.Domain.Categories.Category>()
            .WithMany()
            .HasForeignKey(p => p.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(p => p.Images)
            .WithOne()
            .HasForeignKey(i => i.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.AdditionalCategories)
            .WithOne()
            .HasForeignKey(pc => pc.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        // Metadata for encapsulated collections
        builder.Metadata.FindNavigation(nameof(Product.Images))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.Metadata.FindNavigation(nameof(Product.AdditionalCategories))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        // Indexes
        builder.HasIndex(p => p.Status)
            .HasDatabaseName("IX_Products_Status");

        builder.HasIndex(p => p.CategoryId)
            .HasDatabaseName("IX_Products_CategoryId");

        builder.HasIndex(p => p.IsDeleted)
            .HasFilter("[IsDeleted] = 0")
            .HasDatabaseName("IX_Products_IsDeleted");

        // Ignore domain events collection
        builder.Ignore(p => p.DomainEvents);
    }
}
