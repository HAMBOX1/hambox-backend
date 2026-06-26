using HAMBOX.Modules.Commerce.Domain.Account;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HAMBOX.Modules.Commerce.Infrastructure.Configurations.Account;

/// <summary>
/// Configures the <see cref="WishlistItem"/> entity for Entity Framework Core.
/// </summary>
internal sealed class WishlistItemConfiguration : IEntityTypeConfiguration<WishlistItem>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<WishlistItem> builder)
    {
        builder.ToTable("WishlistItems");

        builder.HasKey(w => w.Id);

        builder.Property(w => w.UserId)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(w => w.ProductId)
            .IsRequired();

        builder.Property(w => w.AddedOnUtc)
            .IsRequired();

        builder.Property(w => w.CreatedOnUtc)
            .IsRequired();

        builder.Property(w => w.ModifiedOnUtc);

        builder.HasIndex(w => new { w.UserId, w.ProductId })
            .IsUnique()
            .HasDatabaseName("IX_WishlistItems_UserId_ProductId");

        builder.HasIndex(w => w.UserId)
            .HasDatabaseName("IX_WishlistItems_UserId");
    }
}
