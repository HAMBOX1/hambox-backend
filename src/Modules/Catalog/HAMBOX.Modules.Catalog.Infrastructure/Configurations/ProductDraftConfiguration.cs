using HAMBOX.Modules.Catalog.Domain.Drafts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HAMBOX.Modules.Catalog.Infrastructure.Configurations;

/// <summary>
/// Configures the <see cref="ProductDraft"/> entity for Entity Framework Core.
/// </summary>
internal sealed class ProductDraftConfiguration : IEntityTypeConfiguration<ProductDraft>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ProductDraft> builder)
    {
        builder.ToTable("ProductDrafts");

        // Primary key
        builder.HasKey(d => d.Id);

        // Properties
        builder.Property(d => d.NameAr)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(d => d.NameEn)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(d => d.DescriptionAr)
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(d => d.DescriptionEn)
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(d => d.Price)
            .HasColumnType("decimal(18,2)");

        builder.Property(d => d.CategoryId);

        builder.Property(d => d.CreatedByUserId)
            .IsRequired();

        // Audit properties (IAuditable)
        builder.Property(d => d.CreatedBy)
            .HasMaxLength(256);

        builder.Property(d => d.ModifiedBy)
            .HasMaxLength(256);

        // Base entity properties
        builder.Property(d => d.CreatedOnUtc)
            .IsRequired();

        builder.Property(d => d.ModifiedOnUtc);

        // Concurrency
        builder.Property<byte[]>("RowVersion")
            .IsRowVersion();

        // Indexes
        builder.HasIndex(d => d.CategoryId)
            .HasDatabaseName("IX_ProductDrafts_CategoryId");

        builder.HasIndex(d => d.CreatedByUserId)
            .HasDatabaseName("IX_ProductDrafts_CreatedByUserId");

        // Ignore domain events collection
        builder.Ignore(d => d.DomainEvents);
    }
}
