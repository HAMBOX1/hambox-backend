using HAMBOX.Application.Loyalty;
using HAMBOX.Application.PlatformSettings;
using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Domain.Loyalty;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Commerce.Application.Loyalty;

internal sealed class LoyaltyRankProvider(ICommerceDbContext db, IPlatformSettingsProvider settings) : ILoyaltyRankProvider
{
    public async Task<string?> GetThemeRankKeyAsync(string userId, CancellationToken cancellationToken = default)
    {
        var features = await settings.GetAsync<FeatureCenterSettingsPayload>(PlatformSettingsCategoryKeys.Features, cancellationToken);
        if (!features.LoyaltyRanks || !features.RankThemes)
        {
            return null;
        }

        var key = await db.CustomerLoyaltyRanks.AsNoTracking()
            .Where(r => r.UserId == userId)
            .Select(r => r.RankKey)
            .FirstOrDefaultAsync(cancellationToken);

        if (key is null or LoyaltyRankKeys.Normal)
        {
            return null;
        }

        return await db.LoyaltyRanks.AsNoTracking().AnyAsync(r => r.Key == key && r.IsVisible, cancellationToken) ? key : null;
    }
}
