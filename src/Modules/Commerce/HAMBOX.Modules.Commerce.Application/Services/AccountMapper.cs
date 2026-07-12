using HAMBOX.Modules.Commerce.Domain.Account;
using HAMBOX.Modules.Commerce.Domain.Enums;
using HAMBOX.Modules.Commerce.Domain.Orders;

namespace HAMBOX.Modules.Commerce.Application.Services;

/// <summary>
/// Resolves membership tiers from lifetime spend.
/// </summary>
internal static class MembershipTierResolver
{
    private static readonly (string Name, decimal Threshold)[] Tiers =
    [
        ("Platinum", 5000m),
        ("Gold", 1500m),
        ("Silver", 500m),
        ("Bronze", 0m)
    ];

    public static (string Tier, decimal NextTierThreshold, decimal ProgressPercent) Resolve(decimal lifetimeSpend)
    {
        for (var i = 0; i < Tiers.Length; i++)
        {
            var (name, threshold) = Tiers[i];

            if (lifetimeSpend >= threshold)
            {
                if (i == 0)
                {
                    return (name, threshold, 100m);
                }

                var previousThreshold = Tiers[i - 1].Threshold;
                var range = previousThreshold - threshold;
                var progress = range <= 0
                    ? 100m
                    : Math.Min(100m, (lifetimeSpend - threshold) / range * 100m);

                return (name, previousThreshold, progress);
            }
        }

        return ("Bronze", 500m, lifetimeSpend / 500m * 100m);
    }
}

/// <summary>
/// Builds account-related DTOs from domain entities.
/// </summary>
internal static class AccountMapper
{
    public const int ReferralPointsPerSuccessfulReferral = 100;

    public static IReadOnlyList<Contracts.Account.ReferralTierDto> GetReferralTiers() =>
    [
        new("Starter", 0, ReferralPointsPerSuccessfulReferral),
        new("Bronze", 100, ReferralPointsPerSuccessfulReferral),
        new("Silver", 500, ReferralPointsPerSuccessfulReferral),
        new("Gold", 1000, ReferralPointsPerSuccessfulReferral)
    ];

    public static Contracts.Account.OrderSummaryDto ToOrderSummaryDto(Order order) =>
        new(
            order.Id,
            order.OrderNumber,
            order.Status.ToString(),
            order.TotalAmount,
            order.Items.Count,
            order.CreatedOnUtc);

    public static IReadOnlyList<Contracts.Account.OrderTimelineEventDto> BuildOrderTimeline(Order order)
    {
        var events = new List<Contracts.Account.OrderTimelineEventDto>
        {
            new("OrderPlaced", "Your order was placed successfully.", order.CreatedOnUtc)
        };

        if (order.Status == OrderStatus.Completed)
        {
            events.Add(new(
                "PaymentConfirmed",
                "Payment was confirmed.",
                order.CreatedOnUtc.AddMinutes(1)));

            events.Add(new(
                "Completed",
                "Your order is complete. License keys are available.",
                order.ModifiedOnUtc ?? order.CreatedOnUtc.AddMinutes(2)));
        }

        return events;
    }

    public static Contracts.Account.ReferralSummaryDto ToReferralSummaryDto(ReferralProfile profile) =>
        new(profile.ReferralCode, profile.Tier, profile.LifetimePoints, profile.SuccessfulReferrals);

    public static Contracts.Account.ReferralHistoryDto ToReferralHistoryDto(ReferralHistoryEntry entry) =>
        new(entry.Id, entry.ReferredUserId, entry.PointsEarned, entry.CreatedOnUtc);

    public static Contracts.Account.ReferralDashboardDto ToReferralDashboardDto(
        ReferralProfile profile,
        IReadOnlyList<ReferralHistoryEntry> recentHistory) =>
        new(
            profile.ReferralCode,
            profile.Tier,
            profile.LifetimePoints,
            profile.SuccessfulReferrals,
            GetReferralTiers(),
            recentHistory.Select(ToReferralHistoryDto).ToList());

    public static Contracts.Account.WishlistItemDto ToWishlistItemDto(
        WishlistItem item,
        string productNameEn,
        decimal unitPrice,
        string? productImageUrl = null) =>
        new(item.Id, item.ProductId, productNameEn, unitPrice, productImageUrl, item.AddedOnUtc);

    public static Contracts.Account.ProductReviewDto ToProductReviewDto(ProductReview review) =>
        new(
            review.Id,
            review.UserId,
            review.ProductId,
            review.OrderId,
            review.Rating,
            review.Comment,
            review.CreatedOnUtc,
            review.ModifiedOnUtc);

    public static Contracts.Account.UserNotificationDto ToUserNotificationDto(UserNotification notification) =>
        new(
            notification.Id,
            notification.Title,
            notification.Body,
            notification.Category,
            notification.IsRead,
            notification.CreatedOnUtc,
            notification.ActionUrl);
}
