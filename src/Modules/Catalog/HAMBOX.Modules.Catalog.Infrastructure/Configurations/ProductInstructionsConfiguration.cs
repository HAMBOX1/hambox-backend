using HAMBOX.Modules.Catalog.Domain.Instructions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HAMBOX.Modules.Catalog.Infrastructure.Configurations;

/// <summary>
/// Configures the <see cref="ProductInstructions"/> entity for Entity Framework Core.
/// </summary>
internal sealed class ProductInstructionsConfiguration : IEntityTypeConfiguration<ProductInstructions>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ProductInstructions> builder)
    {
        builder.ToTable("ProductInstructions");

        builder.HasKey(i => i.Id);

        builder.Property(i => i.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(i => i.ContentHtml)
            .IsRequired()
            .HasColumnType("nvarchar(max)");

        builder.Property(i => i.Version)
            .IsRequired();

        builder.Property(i => i.IsPublished)
            .IsRequired();

        // Audit properties (IAuditable)
        builder.Property(i => i.CreatedBy)
            .HasMaxLength(256);

        builder.Property(i => i.ModifiedBy)
            .HasMaxLength(256);

        // Base entity properties
        builder.Property(i => i.CreatedOnUtc)
            .IsRequired();

        builder.Property(i => i.ModifiedOnUtc);

        // Concurrency
        builder.Property<byte[]>("RowVersion")
            .IsRowVersion();

        // One row per product
        builder.HasIndex(i => i.ProductId)
            .IsUnique()
            .HasDatabaseName("IX_ProductInstructions_ProductId");

        builder.Ignore(i => i.DomainEvents);
    }
}
