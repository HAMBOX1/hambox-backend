using HAMBOX.Modules.Commerce.Domain.Account;
using HAMBOX.Modules.Commerce.Domain.Archive;
using HAMBOX.Modules.Commerce.Domain.Business;
using HAMBOX.Modules.Commerce.Domain.Carts;
using HAMBOX.Modules.Commerce.Domain.Cosmetics;
using HAMBOX.Modules.Commerce.Domain.Idempotency;
using HAMBOX.Modules.Commerce.Domain.Loyalty;
using HAMBOX.Modules.Commerce.Domain.Memberships;
using HAMBOX.Modules.Commerce.Domain.Missions;
using HAMBOX.Modules.Commerce.Domain.Operations;
using HAMBOX.Modules.Commerce.Domain.Orders;
using HAMBOX.Modules.Commerce.Domain.PaymentGateways;
using HAMBOX.Modules.Commerce.Domain.Promotions;
using HAMBOX.Modules.Commerce.Domain.Reports;
using HAMBOX.Modules.Commerce.Domain.Rewards;
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

    /// <summary>Gets the helpful / not-helpful votes on reviews.</summary>
    DbSet<ReviewVote> ReviewVotes { get; }

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
    /// Gets the purchase-points ledger database set (append-only).
    /// </summary>
    DbSet<PointsTransaction> PointsTransactions { get; }

    /// <summary>Gets the admin-managed Stars earn rules database set.</summary>
    DbSet<StarsEarnRule> StarsEarnRules { get; }

    /// <summary>Gets the profile frames, badges and animations.</summary>
    DbSet<ProfileCosmetic> ProfileCosmetics { get; }

    /// <summary>Gets cosmetics bought with Stars.</summary>
    DbSet<CustomerCosmetic> CustomerCosmetics { get; }

    /// <summary>Gets what each customer currently wears.</summary>
    DbSet<CustomerProfileStyle> CustomerProfileStyles { get; }

    /// <summary>Gets the bonus Stars each rule contributed to each order (for budgets and per-customer limits).</summary>
    DbSet<StarsRuleAward> StarsRuleAwards { get; }

    /// <summary>Gets the admin-managed missions and milestones.</summary>
    DbSet<Mission> Missions { get; }

    /// <summary>Gets which customer earned which mission.</summary>
    DbSet<MissionCompletion> MissionCompletions { get; }

    /// <summary>Gets ELITE Business (B2B) accounts.</summary>
    DbSet<BusinessAccount> BusinessAccounts { get; }

    /// <summary>Gets wholesale price-list entries.</summary>
    DbSet<BusinessPrice> BusinessPrices { get; }

    /// <summary>Gets B2B quotes (with their lines).</summary>
    DbSet<BusinessQuote> BusinessQuotes { get; }

    /// <summary>Gets customers carried over from the old HAMBOX archive.</summary>
    DbSet<LegacyCustomerRecord> LegacyCustomerRecords { get; }

    /// <summary>Gets old orders kept as history.</summary>
    DbSet<LegacyOrderRecord> LegacyOrderRecords { get; }

    /// <summary>Gets referral codes carried over from the retired PRIME programme.</summary>
    DbSet<ReferralLegacyCode> ReferralLegacyCodes { get; }

    /// <summary>Gets the Stars Shop reward catalog.</summary>
    DbSet<RewardItem> RewardItems { get; }

    /// <summary>Gets every Stars Shop / monthly-gift redemption.</summary>
    DbSet<RewardRedemption> RewardRedemptions { get; }

    /// <summary>Gets customers' saved Stars goals.</summary>
    DbSet<StarsGoal> StarsGoals { get; }

    /// <summary>Gets the loyalty rank ladder (Normal to Diamond).</summary>
    DbSet<LoyaltyRank> LoyaltyRanks { get; }

    /// <summary>Gets each customer's current loyalty rank.</summary>
    DbSet<CustomerLoyaltyRank> CustomerLoyaltyRanks { get; }

    /// <summary>Gets the append-only loyalty rank change history.</summary>
    DbSet<LoyaltyRankHistory> LoyaltyRankHistory { get; }

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

    DbSet<PaymentWebhookLog> PaymentWebhookLogs { get; }

    DbSet<PaymentAttempt> PaymentAttempts { get; }

    DbSet<PaymentGatewayConfiguration> PaymentGatewayConfigurations { get; }

    DbSet<FloatingAd> FloatingAds { get; }

    DbSet<MembershipPlan> MembershipPlans { get; }
    DbSet<MembershipBenefit> MembershipBenefits { get; }
    DbSet<MembershipPlanProductAccess> MembershipPlanProductAccess { get; }
    DbSet<MembershipSubscription> MembershipSubscriptions { get; }
    DbSet<MembershipHistory> MembershipHistories { get; }
    DbSet<MembershipCode> MembershipCodes { get; }
    DbSet<MemberPriceUsage> MemberPriceUsages { get; }
    DbSet<MembershipCodeRedemption> MembershipCodeRedemptions { get; }
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
