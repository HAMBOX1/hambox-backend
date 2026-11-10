using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Domain.Account;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Commerce.Application.Services;

/// <summary>
/// Finds a customer's referral profile, creating it on first use. The account dashboard loads several endpoints at
/// once, and a new customer's first visit made each of them try to create the same row — the loser hit the unique
/// index and answered 500. The loser now just reads the winner's row.
/// </summary>
internal static class ReferralProfileProvisioner
{
    public static async Task<ReferralProfile> GetOrCreateAsync(
        ICommerceDbContext dbContext,
        string userId,
        CancellationToken cancellationToken)
    {
        var profile = await dbContext.ReferralProfiles.FirstOrDefaultAsync(r => r.UserId == userId, cancellationToken);
        if (profile is not null)
        {
            return profile;
        }

        profile = ReferralProfile.CreateForUser(userId);
        dbContext.ReferralProfiles.Add(profile);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return profile;
        }
        catch (DbUpdateException)
        {
            // A concurrent request created it first: drop ours and use theirs.
            dbContext.ReferralProfiles.Remove(profile);
            return await dbContext.ReferralProfiles.FirstAsync(r => r.UserId == userId, cancellationToken);
        }
    }
}
