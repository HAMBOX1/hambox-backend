using HAMBOX.Modules.Commerce.Domain.Carts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HAMBOX.Modules.Commerce.Infrastructure.Configurations;

/// <summary>
/// Configures the <see cref="ShoppingCart"/> entity for Entity Framework Core.
/// </summary>
internal sealed class ShoppingCartConfiguration : IEntityTypeConfiguration<ShoppingCart>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ShoppingCart> builder)
    {
        builder.ToTable("ShoppingCarts");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.UserId)
            .HasMaxLength(450);

        builder.Property(c => c.GuestSessionId)
            .HasMaxLength(100);

        builder.Property(c => c.AppliedCouponCode)
            .HasMaxLength(50);

        builder.Property(c => c.CreatedOnUtc)
            .IsRequired();

        builder.Property(c => c.ModifiedOnUtc);

        builder.HasMany(c => c.Items)
            .WithOne()
            .HasForeignKey(i => i.ShoppingCartId)
            .OnDelete(DeleteBehavior.Cascade);

        var itemsNavigation = builder.Metadata.FindNavigation(nameof(ShoppingCart.Items))!;
        itemsNavigation.SetPropertyAccessMode(PropertyAccessMode.Field);
        itemsNavigation.SetField("_items");

        builder.HasIndex(c => c.UserId)
            .IsUnique()
            .HasFilter("[UserId] IS NOT NULL")
            .HasDatabaseName("IX_ShoppingCarts_UserId");

        builder.HasIndex(c => c.GuestSessionId)
            .IsUnique()
            .HasFilter("[GuestSessionId] IS NOT NULL")
            .HasDatabaseName("IX_ShoppingCarts_GuestSessionId");

        builder.Ignore(c => c.DomainEvents);
    }
}
