using HAMBOX.Modules.Catalog.Domain.Collections;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HAMBOX.Modules.Catalog.Infrastructure.Configurations;

/// <summary>
/// Configures the <see cref="ProductCollection"/> entity for Entity Framework Core.
/// </summary>
internal sealed class ProductCollectionConfiguration : IEntityTypeConfiguration<ProductCollection>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ProductCollection> builder)
    {
        builder.ToTable("ProductCollections");

        // Primary key
        builder.HasKey(c => c.Id);

        // Properties
        builder.Property(c => c.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(c => c.Description)
            .HasMaxLength(1000);

        builder.Property(c => c.Color)
            .HasMaxLength(20);

        builder.Property(c => c.Icon)
            .HasMaxLength(50);

        builder.Property(c => c.ParentId);

        builder.Property(c => c.SortOrder)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(c => c.IsSystem)
            .IsRequired()
            .HasDefaultValue(false);

        builder.HasIndex(c => c.ParentId)
            .HasDatabaseName("IX_ProductCollections_ParentId");

        // Audit properties (IAuditable)
        builder.Property(c => c.CreatedBy)
            .HasMaxLength(256);

        builder.Property(c => c.ModifiedBy)
            .HasMaxLength(256);

        // Soft delete properties (ISoftDeletable)
        builder.Property(c => c.IsDeleted)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(c => c.DeletedOnUtc);

        // Base entity properties
        builder.Property(c => c.CreatedOnUtc)
            .IsRequired();

        builder.Property(c => c.ModifiedOnUtc);

        // Concurrency
        builder.Property<byte[]>("RowVersion")
            .IsRowVersion();

        // Indexes
        builder.HasIndex(c => c.IsDeleted)
            .HasFilter("[IsDeleted] = 0")
            .HasDatabaseName("IX_ProductCollections_IsDeleted");

        // Ignore domain events collection
        builder.Ignore(c => c.DomainEvents);
    }
}
