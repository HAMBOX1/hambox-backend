using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Domain.Account;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Commerce.Application.Memberships;

/// <summary>
/// Awards referral points with membership plan multiplier applied.
/// </summary>
internal sealed class ReferralRewardService
{
    private readonly ICommerceDbContext _dbContext;
    private readonly IMembershipEngine _membershipEngine;

    public ReferralRewardService(ICommerceDbContext dbContext, IMembershipEngine membershipEngine)
    {
        _dbContext = dbContext;
        _membershipEngine = membershipEngine;
    }

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

        var profile = await _dbContext.ReferralProfiles
            .FirstOrDefaultAsync(r => r.UserId == referrerUserId, cancellationToken);

        if (profile is null)
        {
            profile = ReferralProfile.CreateForUser(referrerUserId);
            _dbContext.ReferralProfiles.Add(profile);
        }

        profile.AwardReferralPoints(multiplied);
        return multiplied;
    }
}
