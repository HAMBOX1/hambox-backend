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
    /// <summary>
    /// Builds the dashboard's tier list from the single centralized threshold table
    /// (<see cref="ReferralTierPolicy"/>) rather than a second, independently-hardcoded copy.
    /// </summary>
    public static IReadOnlyList<Contracts.Account.ReferralTierDto> GetReferralTiers(int pointsPerReferral) =>
        ReferralTierPolicy.Tiers
            .Select(t => new Contracts.Account.ReferralTierDto(t.Name, t.MinimumPoints, pointsPerReferral))
            .ToList();

    public static Contracts.Account.OrderSummaryDto ToOrderSummaryDto(Order order, string? imageUrl = null) =>
        new(
            order.Id,
            order.OrderNumber,
            order.Status.ToString(),
            order.TotalAmount,
            order.Items.Count,
            order.CreatedOnUtc,
            imageUrl);

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

    public static Contracts.Account.ReferralSummaryDto ToReferralSummaryDto(ReferralProfile profile, decimal pointValueUsd) =>
        new(profile.ReferralCode, profile.Tier, profile.LifetimePoints, profile.SuccessfulReferrals, pointValueUsd);

    public static Contracts.Account.ReferralHistoryDto ToReferralHistoryDto(ReferralHistoryEntry entry) =>
        new(
            entry.Id,
            AdminOrderMapper.ResolveCustomerName(entry.ReferredEmail),
            entry.PointsEarned,
            entry.Status.ToString(),
            entry.CreatedOnUtc,
            entry.QualifiedOnUtc,
            entry.RewardedOnUtc);

    public static Contracts.Account.ReferralDashboardDto ToReferralDashboardDto(
        ReferralProfile profile,
        IReadOnlyList<ReferralHistoryEntry> recentHistory,
        int pendingReferrals,
        int pointsPerReferral,
        decimal pointValueUsd) =>
        new(
            profile.ReferralCode,
            profile.Tier,
            profile.LifetimePoints,
            profile.SuccessfulReferrals,
            pendingReferrals,
            pointValueUsd,
            GetReferralTiers(pointsPerReferral),
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
            notification.ActionUrl,
            notification.IsArchived);
}
