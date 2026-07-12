using HAMBOX.Modules.Commerce.Domain.Promotions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HAMBOX.Modules.Commerce.Infrastructure.Configurations.Promotions;

internal sealed class OrderAppliedPromotionConfiguration : IEntityTypeConfiguration<OrderAppliedPromotion>
{
    public void Configure(EntityTypeBuilder<OrderAppliedPromotion> builder)
    {
        builder.ToTable("OrderAppliedPromotions");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.PromotionName).HasMaxLength(200).IsRequired();
        builder.Property(p => p.CouponCode).HasMaxLength(50);
        builder.Property(p => p.DiscountAmount).HasPrecision(18, 2);
        builder.Property(p => p.PromotionType).HasConversion<int>();

        builder.HasIndex(p => p.OrderId).HasDatabaseName("IX_OrderAppliedPromotions_OrderId");
    }
}
