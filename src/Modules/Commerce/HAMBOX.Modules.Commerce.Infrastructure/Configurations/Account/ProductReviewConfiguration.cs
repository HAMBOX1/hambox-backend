using HAMBOX.Modules.Commerce.Domain.Account;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HAMBOX.Modules.Commerce.Infrastructure.Configurations.Account;

/// <summary>
/// Configures the <see cref="ProductReview"/> entity for Entity Framework Core.
/// </summary>
internal sealed class ProductReviewConfiguration : IEntityTypeConfiguration<ProductReview>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ProductReview> builder)
    {
        builder.ToTable("ProductReviews");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.UserId)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(r => r.ProductId)
            .IsRequired();

        builder.Property(r => r.OrderId)
            .IsRequired();

        builder.Property(r => r.Rating)
            .IsRequired();

        builder.Property(r => r.Comment)
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(r => r.CreatedOnUtc)
            .IsRequired();

        builder.Property(r => r.ModifiedOnUtc);

        builder.HasIndex(r => new { r.UserId, r.ProductId })
            .IsUnique()
            .HasDatabaseName("IX_ProductReviews_UserId_ProductId");

        builder.HasIndex(r => r.ProductId)
            .HasDatabaseName("IX_ProductReviews_ProductId");
    }
}
