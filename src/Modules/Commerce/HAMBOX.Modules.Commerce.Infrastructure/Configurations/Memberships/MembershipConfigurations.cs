using HAMBOX.Modules.Commerce.Domain.Memberships;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HAMBOX.Modules.Commerce.Infrastructure.Configurations.Memberships;

internal sealed class MembershipPlanConfiguration : IEntityTypeConfiguration<MembershipPlan>
{
    public void Configure(EntityTypeBuilder<MembershipPlan> builder)
    {
        builder.ToTable("MembershipPlans");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Name).HasMaxLength(200).IsRequired();
        builder.Property(p => p.Slug).HasMaxLength(100).IsRequired();
        builder.Property(p => p.Description).HasMaxLength(2000);
        builder.Property(p => p.Price).HasPrecision(18, 2);
        builder.Property(p => p.BadgeLabel).HasMaxLength(100);
        builder.Property(p => p.ThemeKey).HasMaxLength(100);
        builder.Property(p => p.Status).HasConversion<int>();

        builder.HasIndex(p => p.Slug).IsUnique().HasDatabaseName("IX_MembershipPlans_Slug");
        builder.HasIndex(p => new { p.Status, p.SortOrder }).HasDatabaseName("IX_MembershipPlans_Status_Sort");

        builder.HasMany(p => p.Benefits).WithOne().HasForeignKey(b => b.PlanId).OnDelete(DeleteBehavior.Cascade);
        builder.Metadata.FindNavigation(nameof(MembershipPlan.Benefits))!.SetPropertyAccessMode(PropertyAccessMode.Field);
        builder.Ignore(p => p.DomainEvents);
    }
}

internal sealed class MembershipBenefitConfiguration : IEntityTypeConfiguration<MembershipBenefit>
{
    public void Configure(EntityTypeBuilder<MembershipBenefit> builder)
    {
        builder.ToTable("MembershipBenefits");
        builder.HasKey(b => b.Id);
        builder.Property(b => b.BenefitType).HasConversion<int>();
        builder.Property(b => b.Value).HasMaxLength(2000).IsRequired();
        builder.Property(b => b.DisplayName).HasMaxLength(200).IsRequired();
    }
}

internal sealed class MembershipSubscriptionConfiguration : IEntityTypeConfiguration<MembershipSubscription>
{
    public void Configure(EntityTypeBuilder<MembershipSubscription> builder)
    {
        builder.ToTable("MembershipSubscriptions");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.UserId).HasMaxLength(450).IsRequired();
        builder.Property(s => s.Status).HasConversion<int>();
        builder.HasIndex(s => s.UserId).HasDatabaseName("IX_MembershipSubscriptions_UserId");
        builder.HasIndex(s => new { s.UserId, s.Status }).HasDatabaseName("IX_MembershipSubscriptions_User_Status");
        builder.Ignore(s => s.DomainEvents);
    }
}

internal sealed class MembershipHistoryConfiguration : IEntityTypeConfiguration<MembershipHistory>
{
    public void Configure(EntityTypeBuilder<MembershipHistory> builder)
    {
        builder.ToTable("MembershipHistories");
        builder.HasKey(h => h.Id);
        builder.Property(h => h.UserId).HasMaxLength(450).IsRequired();
        builder.Property(h => h.Action).HasConversion<int>();
        builder.Property(h => h.PerformedByUserId).HasMaxLength(450);
        builder.Property(h => h.Notes).HasMaxLength(2000);
        builder.HasIndex(h => h.UserId).HasDatabaseName("IX_MembershipHistories_UserId");
    }
}

internal sealed class MembershipTransactionConfiguration : IEntityTypeConfiguration<MembershipTransaction>
{
    public void Configure(EntityTypeBuilder<MembershipTransaction> builder)
    {
        builder.ToTable("MembershipTransactions");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.UserId).HasMaxLength(450).IsRequired();
        builder.Property(t => t.Amount).HasPrecision(18, 2);
        builder.Property(t => t.Status).HasConversion<int>();
        builder.Property(t => t.ExternalPaymentId).HasMaxLength(200);
        builder.Property(t => t.OrderId);
        builder.HasIndex(t => t.OrderId).HasDatabaseName("IX_MembershipTransactions_OrderId");
    }
}

internal sealed class MembershipAuditLogConfiguration : IEntityTypeConfiguration<MembershipAuditLog>
{
    public void Configure(EntityTypeBuilder<MembershipAuditLog> builder)
    {
        builder.ToTable("MembershipAuditLogs");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Action).HasConversion<int>();
        builder.Property(a => a.PerformedByUserId).HasMaxLength(450);
        builder.Property(a => a.Details).HasMaxLength(2000);
    }
}
