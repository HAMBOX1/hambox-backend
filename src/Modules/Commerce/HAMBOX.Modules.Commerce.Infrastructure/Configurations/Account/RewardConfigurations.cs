using HAMBOX.Modules.Commerce.Domain.Rewards;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HAMBOX.Modules.Commerce.Infrastructure.Configurations.Account;

internal sealed class RewardItemConfiguration : IEntityTypeConfiguration<RewardItem>
{
    public void Configure(EntityTypeBuilder<RewardItem> builder)
    {
        builder.ToTable("RewardItems");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.MinRankKey).HasMaxLength(30);
        builder.Property(r => r.TitleEn).HasMaxLength(200);
        builder.Property(r => r.TitleAr).HasMaxLength(200);
        builder.Ignore(r => r.IsCoupon);
        builder.HasIndex(r => new { r.IsActive, r.SortOrder }).HasDatabaseName("IX_RewardItems_Active_Sort");
        builder.HasIndex(r => r.ProductVariantId).HasDatabaseName("IX_RewardItems_Variant");
        builder.Ignore(r => r.DomainEvents);
    }
}

internal sealed class RewardRedemptionConfiguration : IEntityTypeConfiguration<RewardRedemption>
{
    public void Configure(EntityTypeBuilder<RewardRedemption> builder)
    {
        builder.ToTable("RewardRedemptions");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.UserId).IsRequired().HasMaxLength(450);
        builder.Property(r => r.PeriodKey).HasMaxLength(20);
        builder.Property(r => r.CouponCode).HasMaxLength(64);
        builder.Property(r => r.Source).HasConversion<int>();
        builder.Property(r => r.Status).HasConversion<int>();
        builder.HasIndex(r => new { r.UserId, r.RewardItemId }).HasDatabaseName("IX_RewardRedemptions_User_Reward");
        builder.HasIndex(r => r.OrderId).HasDatabaseName("IX_RewardRedemptions_Order");

        // One monthly gift per customer per period — enforced by the database, not just the handler.
        builder.HasIndex(r => new { r.UserId, r.PeriodKey })
            .IsUnique()
            .HasFilter("[PeriodKey] IS NOT NULL")
            .HasDatabaseName("UX_RewardRedemptions_User_Period");
    }
}

internal sealed class StarsGoalConfiguration : IEntityTypeConfiguration<StarsGoal>
{
    public void Configure(EntityTypeBuilder<StarsGoal> builder)
    {
        builder.ToTable("StarsGoals");
        builder.HasKey(g => g.Id);
        builder.Property(g => g.UserId).IsRequired().HasMaxLength(450);
        builder.HasIndex(g => new { g.UserId, g.RewardItemId }).IsUnique().HasDatabaseName("UX_StarsGoals_User_Reward");
    }
}
