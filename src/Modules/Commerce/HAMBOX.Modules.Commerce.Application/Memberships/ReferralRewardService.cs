using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Domain.Account;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Commerce.Application.Memberships;

/// <summary>
/// Awards (and reverses) referral points with the membership plan multiplier applied. Mutates
/// <see cref="ReferralProfile"/> exclusively through atomic conditional UPDATEs — the same pattern
/// already used for coupon/promotion redemption-limit updates in checkout — rather than the usual
/// load-mutate-SaveChanges flow, because two different referred users completing their first order
/// at the same instant would otherwise race on the same profile's LifetimePoints/SuccessfulReferrals
/// counters. <see cref="ICommerceDbContext"/> deliberately exposes no change-tracker access to retry
/// a lost optimistic-concurrency conflict, so an atomic UPDATE — which can't lose a race regardless of
/// how many writers contend for it — is the mechanism that actually fits this interface.
/// </summary>
public sealed class ReferralRewardService
{
    private readonly ICommerceDbContext _dbContext;
    private readonly IMembershipEngine _membershipEngine;

    public ReferralRewardService(ICommerceDbContext dbContext, IMembershipEngine membershipEngine)
    {
        _dbContext = dbContext;
        _membershipEngine = membershipEngine;
    }

    /// <summary>
    /// Awards points to a referrer's profile, applying their membership's referral multiplier.
    /// Returns the number of points actually awarded (0 if the referrer no longer has a profile).
    /// </summary>
    public async Task<int> AwardPointsAsync(
        string referrerUserId,
        int basePoints,
        CancellationToken cancellationToken = default)
    {
        if (basePoints <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(basePoints));
        }

        var membership = await _membershipEngine.ResolveAsync(referrerUserId, cancellationToken);
        var multiplied = (int)Math.Round(basePoints * membership.ReferralMultiplier, MidpointRounding.AwayFromZero);

        var updated = await _dbContext.ReferralProfiles
            .Where(p => p.UserId == referrerUserId)
            .ExecuteUpdateAsync(
                s => s
                    .SetProperty(p => p.LifetimePoints, p => p.LifetimePoints + multiplied)
                    .SetProperty(p => p.SuccessfulReferrals, p => p.SuccessfulReferrals + 1),
                cancellationToken);

        if (updated == 0)
        {
            // Redemption always resolves an existing profile before creating a Pending entry, so a
            // missing profile here should be unreachable in practice — defensive no-op only.
            return 0;
        }

        await SyncTierAsync(referrerUserId, cancellationToken);
        return multiplied;
    }

    /// <summary>
    /// Claws back points from a referrer after their referral is reversed (the qualifying order was
    /// refunded or cancelled). Never takes a profile below zero.
    /// </summary>
    public async Task ReverseAsync(string referrerUserId, int points, CancellationToken cancellationToken = default)
    {
        if (points <= 0)
        {
            return;
        }

        var updated = await _dbContext.ReferralProfiles
            .Where(p => p.UserId == referrerUserId)
            .ExecuteUpdateAsync(
                s => s
                    .SetProperty(p => p.LifetimePoints, p => Math.Max(0, p.LifetimePoints - points))
                    .SetProperty(p => p.SuccessfulReferrals, p => Math.Max(0, p.SuccessfulReferrals - 1)),
                cancellationToken);

        if (updated == 0)
        {
            return;
        }

        await SyncTierAsync(referrerUserId, cancellationToken);
    }

    // Tier is a display-only classification derived from LifetimePoints. Recomputing it in a second
    // statement (rather than inlining the threshold ladder into the UPDATE above, which EF can't
    // translate to SQL for an arbitrary C# method) keeps the thresholds defined in exactly one place
    // (ReferralTierPolicy.ResolveTier).
    // ponytail: under back-to-back concurrent awards/reversals for the same profile, this second step
    // can briefly read a total that's already been superseded and set a Tier one step behind —
    // LifetimePoints itself is never lost (the first UPDATE is atomic), and the next award or
    // reversal recomputes Tier from the then-current total, so it self-corrects. Upgrade path: fold
    // the threshold check into the first UPDATE as a translatable expression if Tier staleness ever
    // becomes user-visible enough to matter.
    private async Task SyncTierAsync(string referrerUserId, CancellationToken cancellationToken)
    {
        var lifetimePoints = await _dbContext.ReferralProfiles
            .Where(p => p.UserId == referrerUserId)
            .Select(p => p.LifetimePoints)
            .FirstOrDefaultAsync(cancellationToken);

        await _dbContext.ReferralProfiles
            .Where(p => p.UserId == referrerUserId)
            .ExecuteUpdateAsync(
                s => s.SetProperty(p => p.Tier, ReferralTierPolicy.ResolveTier(lifetimePoints)),
                cancellationToken);
    }
}
