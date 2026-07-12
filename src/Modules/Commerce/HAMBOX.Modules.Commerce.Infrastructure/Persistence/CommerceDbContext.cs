using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Domain.Account;
using HAMBOX.Modules.Commerce.Domain.Carts;
using HAMBOX.Modules.Commerce.Domain.Memberships;
using HAMBOX.Modules.Commerce.Domain.Operations;
using HAMBOX.Modules.Commerce.Domain.Orders;
using HAMBOX.Modules.Commerce.Domain.Promotions;
using HAMBOX.Modules.Commerce.Domain.Reports;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Commerce.Infrastructure.Persistence;

/// <summary>
/// Represents the Entity Framework Core database context for the Commerce module.
/// </summary>
public sealed class CommerceDbContext(DbContextOptions<CommerceDbContext> options)
    : DbContext(options), ICommerceDbContext
{
    /// <inheritdoc />
    public DbSet<ShoppingCart> ShoppingCarts => Set<ShoppingCart>();

    /// <inheritdoc />
    public DbSet<CartItem> CartItems => Set<CartItem>();

    /// <inheritdoc />
    public DbSet<Order> Orders => Set<Order>();

    /// <inheritdoc />
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();

    /// <inheritdoc />
    public DbSet<WishlistItem> WishlistItems => Set<WishlistItem>();

    /// <inheritdoc />
    public DbSet<ProductReview> ProductReviews => Set<ProductReview>();

    /// <inheritdoc />
    public DbSet<UserNotification> UserNotifications => Set<UserNotification>();

    /// <inheritdoc />
    public DbSet<ReferralProfile> ReferralProfiles => Set<ReferralProfile>();

    /// <inheritdoc />
    public DbSet<ReferralHistoryEntry> ReferralHistoryEntries => Set<ReferralHistoryEntry>();

    /// <inheritdoc />
    public DbSet<OrderLicenseKey> OrderLicenseKeys => Set<OrderLicenseKey>();

    /// <inheritdoc />
    public DbSet<Promotion> Promotions => Set<Promotion>();

    /// <inheritdoc />
    public DbSet<PromotionCondition> PromotionConditions => Set<PromotionCondition>();

    /// <inheritdoc />
    public DbSet<PromotionTarget> PromotionTargets => Set<PromotionTarget>();

    /// <inheritdoc />
    public DbSet<CouponCode> CouponCodes => Set<CouponCode>();

    /// <inheritdoc />
    public DbSet<PromotionRedemption> PromotionRedemptions => Set<PromotionRedemption>();

    /// <inheritdoc />
    public DbSet<PromotionAuditLog> PromotionAuditLogs => Set<PromotionAuditLog>();

    /// <inheritdoc />
    public DbSet<OrderAppliedPromotion> OrderAppliedPromotions => Set<OrderAppliedPromotion>();

    public DbSet<OrderAdminNote> OrderAdminNotes => Set<OrderAdminNote>();

    public DbSet<OrderAuditEntry> OrderAuditEntries => Set<OrderAuditEntry>();

    public DbSet<OrderPaymentCallback> OrderPaymentCallbacks => Set<OrderPaymentCallback>();

    public DbSet<MembershipPlan> MembershipPlans => Set<MembershipPlan>();
    public DbSet<MembershipBenefit> MembershipBenefits => Set<MembershipBenefit>();
    public DbSet<MembershipSubscription> MembershipSubscriptions => Set<MembershipSubscription>();
    public DbSet<MembershipHistory> MembershipHistories => Set<MembershipHistory>();
    public DbSet<MembershipTransaction> MembershipTransactions => Set<MembershipTransaction>();
    public DbSet<MembershipAuditLog> MembershipAuditLogs => Set<MembershipAuditLog>();

    public DbSet<OperationalJob> OperationalJobs => Set<OperationalJob>();

    public DbSet<ApiRequestLog> ApiRequestLogs => Set<ApiRequestLog>();

    public DbSet<OperationalAuditLog> OperationalAuditLogs => Set<OperationalAuditLog>();

    public DbSet<OperationalAlert> OperationalAlerts => Set<OperationalAlert>();

    public DbSet<ReportDefinition> ReportDefinitions => Set<ReportDefinition>();
    public DbSet<ReportFavorite> ReportFavorites => Set<ReportFavorite>();
    public DbSet<ReportDownload> ReportDownloads => Set<ReportDownload>();
    public DbSet<ScheduledReport> ScheduledReports => Set<ScheduledReport>();
    public DbSet<ScheduledReportExecution> ScheduledReportExecutions => Set<ScheduledReportExecution>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasDefaultSchema("commerce");

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CommerceDbContext).Assembly);
    }
}
