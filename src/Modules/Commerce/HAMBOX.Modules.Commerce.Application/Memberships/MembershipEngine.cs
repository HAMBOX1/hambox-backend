using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Memberships.Models;
using HAMBOX.Modules.Commerce.Domain.Memberships;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Commerce.Application.Memberships;

internal sealed class MembershipEngine : IMembershipEngine
{
    private readonly ICommerceDbContext _dbContext;

    public MembershipEngine(ICommerceDbContext dbContext) => _dbContext = dbContext;

    public async Task<MembershipSnapshot> ResolveAsync(string? userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return MembershipSnapshot.None;
        }

        var subscription = await _dbContext.MembershipSubscriptions
            .Where(s => s.UserId == userId && s.Status == MembershipSubscriptionStatus.Active)
            .OrderByDescending(s => s.ExpiresOnUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (subscription is null)
        {
            return await ResolveDefaultPlanSnapshotAsync(cancellationToken);
        }

        if (!subscription.IsActiveAt(DateTime.UtcNow))
        {
            subscription.MarkExpired();
            await _dbContext.SaveChangesAsync(cancellationToken);
            return await ResolveDefaultPlanSnapshotAsync(cancellationToken);
        }

        var plan = await _dbContext.MembershipPlans
            .Include(p => p.Benefits)
            .FirstOrDefaultAsync(p => p.Id == subscription.PlanId, cancellationToken);

        if (plan is null || plan.Status != MembershipPlanStatus.Active)
        {
            return MembershipSnapshot.None;
        }

        return BuildSnapshot(subscription, plan);
    }

    public async Task ProcessExpirationsAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var expiring = await _dbContext.MembershipSubscriptions
            .Where(s => s.Status == MembershipSubscriptionStatus.Active && s.ExpiresOnUtc <= now)
            .ToListAsync(cancellationToken);

        foreach (var subscription in expiring)
        {
            subscription.MarkExpired();
            _dbContext.MembershipHistories.Add(MembershipHistory.Create(
                subscription.Id,
                subscription.UserId,
                subscription.PlanId,
                MembershipHistoryAction.Expired,
                notes: "Membership expired automatically."));
        }

        if (expiring.Count > 0)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task<MembershipSnapshot> ResolveDefaultPlanSnapshotAsync(CancellationToken cancellationToken)
    {
        var defaultPlan = await _dbContext.MembershipPlans
            .Include(p => p.Benefits)
            .FirstOrDefaultAsync(p => p.IsDefault && p.Status == MembershipPlanStatus.Active, cancellationToken);

        if (defaultPlan is null)
        {
            return MembershipSnapshot.None;
        }

        return BuildSnapshot(null, defaultPlan);
    }

    private static MembershipSnapshot BuildSnapshot(MembershipSubscription? subscription, MembershipPlan plan)
    {
        decimal? discount = null;
        decimal referralMultiplier = 1m;
        int? maxPurchasesPerMonth = null;
        var earlyAccessDays = 0;
        var hasPrioritySupport = false;
        var featureFlags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var benefits = new List<ResolvedMembershipBenefitDto>();

        foreach (var benefit in plan.Benefits.OrderBy(b => b.SortOrder))
        {
            benefits.Add(new ResolvedMembershipBenefitDto(
                benefit.BenefitType.ToString(),
                benefit.Value,
                benefit.DisplayName));

            switch (benefit.BenefitType)
            {
                case MembershipBenefitType.DiscountPercentage when decimal.TryParse(benefit.Value, out var pct):
                    discount = pct;
                    break;
                case MembershipBenefitType.ReferralMultiplier when decimal.TryParse(benefit.Value, out var mult):
                    referralMultiplier = mult;
                    break;
                case MembershipBenefitType.FeatureFlag:
                    featureFlags[benefit.DisplayName] = benefit.Value;
                    break;
                case MembershipBenefitType.MaxPurchasesPerMonth when int.TryParse(benefit.Value, out var limit):
                    maxPurchasesPerMonth = limit;
                    break;
                case MembershipBenefitType.EarlyAccess when int.TryParse(benefit.Value, out var days):
                    earlyAccessDays = days;
                    break;
                case MembershipBenefitType.PrioritySupport:
                    hasPrioritySupport = benefit.Value.Equals("true", StringComparison.OrdinalIgnoreCase);
                    break;
            }
        }

        return new MembershipSnapshot(
            subscription?.Id ?? Guid.Empty,
            plan.Id,
            plan.Name,
            plan.Slug,
            plan.BadgeLabel,
            subscription?.ExpiresOnUtc ?? DateTime.UtcNow.AddYears(10),
            discount,
            referralMultiplier,
            benefits,
            featureFlags,
            maxPurchasesPerMonth,
            earlyAccessDays,
            hasPrioritySupport);
    }
}
