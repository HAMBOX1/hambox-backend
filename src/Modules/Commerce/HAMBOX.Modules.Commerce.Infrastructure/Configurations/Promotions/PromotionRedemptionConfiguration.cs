using HAMBOX.Modules.Commerce.Domain.Promotions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HAMBOX.Modules.Commerce.Infrastructure.Configurations.Promotions;

internal sealed class PromotionRedemptionConfiguration : IEntityTypeConfiguration<PromotionRedemption>
{
    public void Configure(EntityTypeBuilder<PromotionRedemption> builder)
    {
        builder.ToTable("PromotionRedemptions");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.UserId).HasMaxLength(450);
        builder.Property(r => r.PromotionName).HasMaxLength(200).IsRequired();
        builder.Property(r => r.DiscountAmount).HasPrecision(18, 2);
        builder.Property(r => r.RedeemedOnUtc).IsRequired();

        builder.HasIndex(r => r.PromotionId).HasDatabaseName("IX_PromotionRedemptions_PromotionId");
        builder.HasIndex(r => r.OrderId).HasDatabaseName("IX_PromotionRedemptions_OrderId");
        builder.HasIndex(r => r.UserId).HasDatabaseName("IX_PromotionRedemptions_UserId");
    }
}
