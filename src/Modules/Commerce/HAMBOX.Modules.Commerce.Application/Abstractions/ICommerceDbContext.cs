using HAMBOX.Modules.Commerce.Domain.Account;
using HAMBOX.Modules.Commerce.Domain.Carts;
using HAMBOX.Modules.Commerce.Domain.Idempotency;
using HAMBOX.Modules.Commerce.Domain.Memberships;
using HAMBOX.Modules.Commerce.Domain.Operations;
using HAMBOX.Modules.Commerce.Domain.Orders;
using HAMBOX.Modules.Commerce.Domain.Promotions;
using HAMBOX.Modules.Commerce.Domain.Reports;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Commerce.Application.Abstractions;

/// <summary>
/// Defines the database context contract for the Commerce module.
/// </summary>
public interface ICommerceDbContext
{
    /// <summary>
    /// Gets the shopping carts database set.
    /// </summary>
    DbSet<ShoppingCart> ShoppingCarts { get; }

    /// <summary>
    /// Gets the cart items database set.
    /// </summary>
    DbSet<CartItem> CartItems { get; }

    /// <summary>
    /// Gets the orders database set.
    /// </summary>
    DbSet<Order> Orders { get; }

    /// <summary>
    /// Gets the order items database set.
    /// </summary>
    DbSet<OrderItem> OrderItems { get; }

    /// <summary>
    /// Gets the wishlist items database set.
    /// </summary>
    DbSet<WishlistItem> WishlistItems { get; }

    /// <summary>
    /// Gets the customer back-in-stock/price-drop alert subscriptions database set.
    /// </summary>
    DbSet<CustomerAlertSubscription> CustomerAlertSubscriptions { get; }

    /// <summary>
    /// Gets the product reviews database set.
    /// </summary>
    DbSet<ProductReview> ProductReviews { get; }

    /// <summary>
    /// Gets the user notifications database set.
    /// </summary>
    DbSet<UserNotification> UserNotifications { get; }

    /// <summary>
    /// Gets the referral profiles database set.
    /// </summary>
    DbSet<ReferralProfile> ReferralProfiles { get; }

    /// <summary>
    /// Gets the referral history entries database set.
    /// </summary>
    DbSet<ReferralHistoryEntry> ReferralHistoryEntries { get; }

    /// <summary>
    /// Gets the referral audit logs database set.
    /// </summary>
    DbSet<ReferralAuditLog> ReferralAuditLogs { get; }

    /// <summary>
    /// Gets the order license keys database set.
    /// </summary>
    DbSet<OrderLicenseKey> OrderLicenseKeys { get; }

    /// <summary>
    /// Gets the promotions database set.
    /// </summary>
    DbSet<Promotion> Promotions { get; }

    /// <summary>
    /// Gets the promotion conditions database set.
    /// </summary>
    DbSet<PromotionCondition> PromotionConditions { get; }

    /// <summary>
    /// Gets the promotion targets database set.
    /// </summary>
    DbSet<PromotionTarget> PromotionTargets { get; }

    /// <summary>
    /// Gets the coupon codes database set.
    /// </summary>
    DbSet<CouponCode> CouponCodes { get; }

    /// <summary>
    /// Gets the promotion redemptions database set.
    /// </summary>
    DbSet<PromotionRedemption> PromotionRedemptions { get; }

    /// <summary>
    /// Gets the promotion audit logs database set.
    /// </summary>
    DbSet<PromotionAuditLog> PromotionAuditLogs { get; }

    /// <summary>
    /// Gets the order applied promotions database set.
    /// </summary>
    DbSet<OrderAppliedPromotion> OrderAppliedPromotions { get; }

    DbSet<OrderAdminNote> OrderAdminNotes { get; }

    DbSet<OrderAuditEntry> OrderAuditEntries { get; }

    DbSet<OrderPaymentCallback> OrderPaymentCallbacks { get; }

    DbSet<PaymentAttempt> PaymentAttempts { get; }

    DbSet<MembershipPlan> MembershipPlans { get; }
    DbSet<MembershipBenefit> MembershipBenefits { get; }
    DbSet<MembershipPlanProductAccess> MembershipPlanProductAccess { get; }
    DbSet<MembershipSubscription> MembershipSubscriptions { get; }
    DbSet<MembershipHistory> MembershipHistories { get; }
    DbSet<MembershipTransaction> MembershipTransactions { get; }
    DbSet<MembershipAuditLog> MembershipAuditLogs { get; }

    DbSet<OperationalJob> OperationalJobs { get; }

    DbSet<BackgroundJobExecutionHistory> BackgroundJobExecutionHistory { get; }

    DbSet<ApiRequestLog> ApiRequestLogs { get; }

    DbSet<OperationalAuditLog> OperationalAuditLogs { get; }

    DbSet<OperationalAlert> OperationalAlerts { get; }

    DbSet<ReportDefinition> ReportDefinitions { get; }
    DbSet<ReportFavorite> ReportFavorites { get; }
    DbSet<ReportDownload> ReportDownloads { get; }
    DbSet<ScheduledReport> ScheduledReports { get; }
    DbSet<ScheduledReportExecution> ScheduledReportExecutions { get; }

    DbSet<IdempotencyKey> IdempotencyKeys { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
