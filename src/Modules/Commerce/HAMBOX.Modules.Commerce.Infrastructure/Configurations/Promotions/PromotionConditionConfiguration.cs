using HAMBOX.Modules.Commerce.Domain.Promotions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HAMBOX.Modules.Commerce.Infrastructure.Configurations.Promotions;

internal sealed class PromotionConditionConfiguration : IEntityTypeConfiguration<PromotionCondition>
{
    public void Configure(EntityTypeBuilder<PromotionCondition> builder)
    {
        builder.ToTable("PromotionConditions");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.ConditionType).HasConversion<int>();
        builder.Property(c => c.Value).HasMaxLength(500).IsRequired();

        builder.HasIndex(c => new { c.PromotionId, c.ConditionType })
            .HasDatabaseName("IX_PromotionConditions_PromotionId_Type");
    }
}
