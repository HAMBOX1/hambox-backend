using HAMBOX.Modules.Commerce.Domain.Loyalty;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HAMBOX.Modules.Commerce.Infrastructure.Configurations.Account;

internal sealed class LoyaltyRankConfiguration : IEntityTypeConfiguration<LoyaltyRank>
{
    public void Configure(EntityTypeBuilder<LoyaltyRank> builder)
    {
        builder.ToTable("LoyaltyRanks");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Key).IsRequired().HasMaxLength(30);
        builder.Property(r => r.NameEn).IsRequired().HasMaxLength(60);
        builder.Property(r => r.NameAr).IsRequired().HasMaxLength(60);
        builder.Property(r => r.Threshold).HasPrecision(18, 2);
        builder.Property(r => r.AccentColor).HasMaxLength(20);
        builder.HasIndex(r => r.Key).IsUnique().HasDatabaseName("UX_LoyaltyRanks_Key");
    }
}

internal sealed class CustomerLoyaltyRankConfiguration : IEntityTypeConfiguration<CustomerLoyaltyRank>
{
    public void Configure(EntityTypeBuilder<CustomerLoyaltyRank> builder)
    {
        builder.ToTable("CustomerLoyaltyRanks");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.UserId).IsRequired().HasMaxLength(450);
        builder.Property(r => r.RankKey).IsRequired().HasMaxLength(30);
        builder.HasIndex(r => r.UserId).IsUnique().HasDatabaseName("UX_CustomerLoyaltyRanks_UserId");
    }
}

internal sealed class LoyaltyRankHistoryConfiguration : IEntityTypeConfiguration<LoyaltyRankHistory>
{
    public void Configure(EntityTypeBuilder<LoyaltyRankHistory> builder)
    {
        builder.ToTable("LoyaltyRankHistory");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.UserId).IsRequired().HasMaxLength(450);
        builder.Property(r => r.FromRankKey).HasMaxLength(30);
        builder.Property(r => r.ToRankKey).IsRequired().HasMaxLength(30);
        builder.Property(r => r.Reason).IsRequired().HasMaxLength(500);
        builder.Property(r => r.ActorUserId).HasMaxLength(450);
        builder.HasIndex(r => new { r.UserId, r.OccurredOnUtc }).HasDatabaseName("IX_LoyaltyRankHistory_User_Date");
    }
}
