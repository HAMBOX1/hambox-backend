namespace HAMBOX.Modules.Commerce.Application.Contracts.Account;

using HAMBOX.Modules.Commerce.Application.Contracts.Memberships;

/// <summary>
/// Represents a wishlist item.
/// </summary>
public sealed record WishlistItemDto(
    Guid Id,
    Guid ProductId,
    string ProductNameEn,
    decimal UnitPrice,
    string? ProductImageUrl,
    DateTimeOffset AddedOnUtc);

/// <summary>
/// Represents a customer's back-in-stock or price-drop alert subscription for one purchasable
/// variant.
/// </summary>
public sealed record CustomerAlertSubscriptionDto(
    Guid Id,
    string AlertType,
    Guid ProductId,
    string ProductNameEn,
    Guid VariantId,
    string? VariantSku,
    string? ProductImageUrl,
    decimal? LastObservedPrice,
    DateTimeOffset CreatedOnUtc);

/// <summary>
/// Represents a product review.
/// </summary>
public sealed record ProductReviewDto(
    Guid Id,
    string UserId,
    Guid ProductId,
    Guid OrderId,
    int Rating,
    string Comment,
    DateTimeOffset CreatedOnUtc,
    DateTimeOffset? ModifiedOnUtc);

/// <summary>
/// Represents a user notification.
/// </summary>
public sealed record UserNotificationDto(
    Guid Id,
    string Title,
    string Body,
    string Category,
    bool IsRead,
    DateTimeOffset CreatedOnUtc,
    string? ActionUrl = null,
    bool IsArchived = false);

/// <summary>
/// Represents a customer-owned license key reveal response.
/// </summary>
public sealed record RevealCustomerLibraryKeyDto(string LicenseKey);

/// <summary>
/// Represents a referral tier definition.
/// </summary>
public sealed record ReferralTierDto(
    string Name,
    int MinimumPoints,
    int PointsPerReferral);

/// <summary>
/// Represents a referral history entry.
/// </summary>
public sealed record ReferralHistoryDto(
    Guid Id,
    string FriendName,
    int PointsEarned,
    string Status,
    DateTimeOffset CreatedOnUtc,
    DateTime? QualifiedOnUtc,
    DateTime? RewardedOnUtc);

/// <summary>
/// Represents the referral dashboard.
/// </summary>
public sealed record ReferralDashboardDto(
    string ReferralCode,
    string Tier,
    int LifetimePoints,
    int SuccessfulReferrals,
    int PendingReferrals,
    decimal PointValueUsd,
    IReadOnlyList<ReferralTierDto> AvailableTiers,
    IReadOnlyList<ReferralHistoryDto> RecentHistory);

/// <summary>
/// Represents membership information from the active plan subscription.
/// </summary>
public sealed record MembershipCardDto(
    string PlanName,
    string? BadgeLabel,
    string Status,
    DateTime? ExpiresOnUtc,
    decimal? DiscountPercent,
    decimal ReferralMultiplier,
    decimal LifetimeSpend,
    IReadOnlyList<MembershipBenefitDto> Benefits);

/// <summary>
/// Represents a wishlist preview item on the account dashboard.
/// </summary>
public sealed record WishlistPreviewItemDto(
    Guid ProductId,
    string ProductNameEn,
    decimal UnitPrice,
    string? ProductImageUrl,
    DateTimeOffset AddedOnUtc);

/// <summary>
/// Represents referral summary on the account dashboard.
/// </summary>
public sealed record ReferralSummaryDto(
    string ReferralCode,
    string Tier,
    int LifetimePoints,
    int SuccessfulReferrals,
    decimal PointValueUsd);

/// <summary>
/// Represents a recent account activity item.
/// </summary>
public sealed record AccountActivityItemDto(
    string Type,
    string Title,
    string Description,
    DateTimeOffset OccurredOnUtc);

/// <summary>
/// Represents the account dashboard aggregate.
/// </summary>
public sealed record AccountDashboardDto(
    MembershipCardDto Membership,
    IReadOnlyList<WishlistPreviewItemDto> WishlistPreview,
    ReferralSummaryDto Referral,
    IReadOnlyList<AccountActivityItemDto> RecentActivity,
    int UnreadNotificationCount);

/// <summary>
/// Represents an order summary for list views.
/// </summary>
public sealed record OrderSummaryDto(
    Guid Id,
    string OrderNumber,
    string Status,
    decimal TotalAmount,
    int ItemCount,
    DateTimeOffset CreatedOnUtc,
    string? ImageUrl = null);

/// <summary>
/// Represents a timeline event on an order.
/// </summary>
public sealed record OrderTimelineEventDto(
    string EventType,
    string Description,
    DateTimeOffset OccurredOnUtc);

/// <summary>
/// Represents a license key issued for an order item.
/// </summary>
public sealed record OrderLicenseKeyDto(
    Guid OrderItemId,
    Guid ProductId,
    string ProductNameEn,
    string MaskedLicenseKey,
    Guid LicenseKeyId);

/// <summary>
/// Represents review eligibility for an order item.
/// </summary>
public sealed record OrderItemReviewStatusDto(
    Guid OrderItemId,
    Guid ProductId,
    string ProductNameEn,
    bool CanReview,
    bool HasReview,
    Guid? ReviewId);

/// <summary>
/// Represents detailed order information for the account area.
/// </summary>
public sealed record OrderDetailDto(
    Guid Id,
    string OrderNumber,
    string Status,
    string Email,
    string Country,
    string PaymentMethod,
    decimal Subtotal,
    decimal DiscountAmount,
    decimal TaxAmount,
    decimal TotalAmount,
    IReadOnlyList<Contracts.OrderItemDto> Items,
    IReadOnlyList<OrderTimelineEventDto> Timeline,
    IReadOnlyList<OrderLicenseKeyDto> LicenseKeys,
    string? InvoiceUrl,
    string? SupportUrl,
    IReadOnlyList<OrderItemReviewStatusDto> ItemReviewStatuses,
    DateTimeOffset CreatedOnUtc);

/// <summary>
/// Represents a purchased digital product in the customer library.
/// </summary>
public sealed record CustomerLibraryItemDto(
    Guid Id,
    Guid OrderId,
    Guid OrderItemId,
    Guid ProductId,
    Guid? ProductVariantId,
    string ProductNameEn,
    string? ProductImageUrl,
    string? Platform,
    string? Edition,
    string? Region,
    DateTimeOffset PurchaseDate,
    string OrderNumber,
    string OrderStatus,
    string DeliveryStatus,
    string MaskedLicenseKey,
    bool CanReveal,
    string? RedeemInstructions,
    string? InvoiceUrl,
    string? SupportUrl,
    bool IsRecentPurchase,
    bool HasInstructions);

/// <summary>
/// Represents the private, published post-purchase documentation for a library item, as seen by the
/// customer who purchased it.
/// </summary>
public sealed record LibraryItemInstructionsDto(
    string Title,
    string ContentHtml,
    int Version,
    DateTimeOffset? UpdatedOnUtc);
