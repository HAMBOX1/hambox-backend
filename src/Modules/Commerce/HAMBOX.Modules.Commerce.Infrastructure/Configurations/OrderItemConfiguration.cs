using HAMBOX.Modules.Commerce.Domain.Orders;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HAMBOX.Modules.Commerce.Infrastructure.Configurations;

/// <summary>
/// Configures the <see cref="OrderItem"/> entity for Entity Framework Core.
/// </summary>
internal sealed class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<OrderItem> builder)
    {
        builder.ToTable("OrderItems");

        builder.HasKey(i => i.Id);

        builder.Property(i => i.ProductId);

        builder.Property(i => i.MembershipPlanId);

        builder.Property(i => i.LineItemType)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(i => i.ProductNameEn)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(i => i.Quantity)
            .IsRequired();

        builder.Property(i => i.UnitPrice)
            .IsRequired()
            .HasColumnType("decimal(18,2)");

        builder.Property(i => i.LineTotal)
            .IsRequired()
            .HasColumnType("decimal(18,2)");

        builder.Property(i => i.VariantSku).HasMaxLength(100);

        builder.Property(i => i.CreatedOnUtc)
            .IsRequired();

        builder.Property(i => i.ModifiedOnUtc);
    }
}
