using HAMBOX.Modules.Commerce.Domain.Promotions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HAMBOX.Modules.Commerce.Infrastructure.Configurations.Promotions;

internal sealed class CouponCodeConfiguration : IEntityTypeConfiguration<CouponCode>
{
    public void Configure(EntityTypeBuilder<CouponCode> builder)
    {
        builder.ToTable("CouponCodes");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Code).HasMaxLength(50).IsRequired();
        builder.Property(c => c.IsActive).IsRequired();
        builder.Property(c => c.UsedCount).IsRequired();

        builder.HasIndex(c => c.Code)
            .IsUnique()
            .HasDatabaseName("IX_CouponCodes_Code");

        builder.HasIndex(c => c.PromotionId)
            .HasDatabaseName("IX_CouponCodes_PromotionId");
    }
}
