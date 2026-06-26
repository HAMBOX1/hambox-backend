namespace HAMBOX.Modules.Commerce.Application.Contracts.Account;

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
    DateTimeOffset CreatedOnUtc);

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
    string ReferredUserId,
    int PointsEarned,
    DateTimeOffset CreatedOnUtc);

/// <summary>
/// Represents the referral dashboard.
/// </summary>
public sealed record ReferralDashboardDto(
    string ReferralCode,
    string Tier,
    int LifetimePoints,
    int SuccessfulReferrals,
    IReadOnlyList<ReferralTierDto> AvailableTiers,
    IReadOnlyList<ReferralHistoryDto> RecentHistory);

/// <summary>
/// Represents membership information derived from order totals.
/// </summary>
public sealed record MembershipCardDto(
    string Tier,
    decimal LifetimeSpend,
    decimal NextTierThreshold,
    decimal ProgressPercent);

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
    int SuccessfulReferrals);

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
    DateTimeOffset CreatedOnUtc);

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
    string LicenseKey);

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
