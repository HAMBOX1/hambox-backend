using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Contracts.Memberships;
using HAMBOX.Modules.Commerce.Domain.Account;
using HAMBOX.Modules.Commerce.Domain.Memberships;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Commerce.Application.Memberships;

internal static class MembershipMapper
{
    public static MembershipBenefitDto ToBenefitDto(MembershipBenefit benefit) =>
        new(benefit.BenefitType.ToString(), benefit.Value, benefit.DisplayName, benefit.SortOrder);

    public static MembershipPlanDetailDto ToDetailDto(MembershipPlan plan, IReadOnlyList<Guid>? unlockedThemeIds = null) =>
        new(
            plan.Id,
            plan.Name,
            plan.Slug,
            plan.Description,
            plan.Price,
            plan.DurationDays,
            plan.Status.ToString(),
            plan.SortOrder,
            plan.BadgeLabel,
            plan.ThemeKey,
            plan.IsDefault,
            plan.Benefits.Select(ToBenefitDto).ToList(),
            plan.ExclusiveProductAccess.Select(a => a.ProductId).ToList(),
            unlockedThemeIds ?? [],
            plan.CreatedOnUtc);

    public static MembershipSubscriptionDto ToSubscriptionDto(MembershipSubscription subscription, MembershipPlan plan) =>
        new(
            subscription.Id,
            subscription.UserId,
            plan.Id,
            plan.Name,
            subscription.Status.ToString(),
            subscription.StartsOnUtc,
            subscription.ExpiresOnUtc,
            subscription.AutoRenew,
            plan.BadgeLabel);

    public static MembershipHistoryDto ToHistoryDto(MembershipHistory history, string planName) =>
        new(
            history.Id,
            history.PlanId,
            planName,
            history.Action.ToString(),
            history.PreviousPlanId,
            history.Notes,
            history.OccurredOnUtc);

    public static IReadOnlyList<(MembershipBenefitType Type, string Value, string DisplayName, int SortOrder)> ParseBenefits(
        IReadOnlyList<MembershipBenefitDto>? benefits) =>
        benefits is null || benefits.Count == 0
            ? []
            : benefits.Select(b => (
                Enum.Parse<MembershipBenefitType>(b.Type, ignoreCase: true),
                b.Value,
                b.DisplayName,
                b.SortOrder)).ToList();

    public static async Task<Dictionary<Guid, int>> GetSubscriberCountsAsync(
        ICommerceDbContext dbContext,
        IEnumerable<Guid> planIds,
        CancellationToken cancellationToken)
    {
        var ids = planIds.ToList();
        if (ids.Count == 0)
        {
            return [];
        }

        return await dbContext.MembershipSubscriptions
            .Where(s => ids.Contains(s.PlanId) && s.Status == MembershipSubscriptionStatus.Active)
            .GroupBy(s => s.PlanId)
            .Select(g => new { PlanId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.PlanId, x => x.Count, cancellationToken);
    }
}

internal static class MembershipAuditWriter
{
    public static void Write(
        ICommerceDbContext dbContext,
        Guid? planId,
        Guid? subscriptionId,
        MembershipAuditAction action,
        string? userId,
        string? details = null) =>
        dbContext.MembershipAuditLogs.Add(MembershipAuditLog.Create(planId, subscriptionId, action, userId, details));
}

internal static class MembershipNotificationWriter
{
    public static void Notify(
        ICommerceDbContext dbContext,
        string userId,
        string title,
        string body,
        string category) =>
        dbContext.UserNotifications.Add(UserNotification.Create(userId, title, body, category));
}
