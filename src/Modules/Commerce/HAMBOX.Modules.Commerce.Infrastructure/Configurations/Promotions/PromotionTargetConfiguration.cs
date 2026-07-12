using HAMBOX.Modules.Commerce.Domain.Promotions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HAMBOX.Modules.Commerce.Infrastructure.Configurations.Promotions;

internal sealed class PromotionTargetConfiguration : IEntityTypeConfiguration<PromotionTarget>
{
    public void Configure(EntityTypeBuilder<PromotionTarget> builder)
    {
        builder.ToTable("PromotionTargets");

        builder.HasKey(t => t.Id);
        builder.Property(t => t.TargetType).HasConversion<int>();

        builder.HasIndex(t => new { t.PromotionId, t.TargetType, t.TargetId })
            .HasDatabaseName("IX_PromotionTargets_Promotion_Target");
    }
}
