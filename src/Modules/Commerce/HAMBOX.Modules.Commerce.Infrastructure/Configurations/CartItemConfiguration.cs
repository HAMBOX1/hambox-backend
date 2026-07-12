using HAMBOX.Modules.Commerce.Domain.Carts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HAMBOX.Modules.Commerce.Infrastructure.Configurations;

/// <summary>
/// Configures the <see cref="CartItem"/> entity for Entity Framework Core.
/// </summary>
internal sealed class CartItemConfiguration : IEntityTypeConfiguration<CartItem>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<CartItem> builder)
    {
        builder.ToTable("CartItems");

        builder.HasKey(i => i.Id);

        builder.Property(i => i.ProductId)
            .IsRequired();

        builder.Property(i => i.Quantity)
            .IsRequired();

        builder.Property(i => i.UnitPrice)
            .IsRequired()
            .HasColumnType("decimal(18,2)");

        builder.Property(i => i.CreatedOnUtc)
            .IsRequired();

        builder.Property(i => i.ModifiedOnUtc);

        builder.Property(i => i.ProductVariantId);

        builder.HasIndex(i => new { i.ShoppingCartId, i.ProductId, i.ProductVariantId })
            .IsUnique()
            .HasDatabaseName("IX_CartItems_ShoppingCartId_ProductId_VariantId")
            .HasFilter("[ProductVariantId] IS NOT NULL");

        builder.HasIndex(i => new { i.ShoppingCartId, i.ProductId })
            .IsUnique()
            .HasDatabaseName("IX_CartItems_ShoppingCartId_ProductId_NoVariant")
            .HasFilter("[ProductVariantId] IS NULL");
    }
}
