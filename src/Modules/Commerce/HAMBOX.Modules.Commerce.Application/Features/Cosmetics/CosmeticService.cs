using HAMBOX.Application.Membership;
using HAMBOX.Application.PlatformSettings;
using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Loyalty;
using HAMBOX.Modules.Commerce.Application.Points;
using HAMBOX.Modules.Commerce.Domain.Account;
using HAMBOX.Modules.Commerce.Domain.Cosmetics;
using HAMBOX.SharedKernel.Errors;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Commerce.Application.Features.Cosmetics;

public static class CosmeticErrors
{
    public static readonly Error Disabled = new("Cosmetics.Disabled", "Profile customisation is not available right now.");
    public static readonly Error NotFound = new("Cosmetics.NotFound", "That item was not found.");
    public static readonly Error Locked = new("Cosmetics.Locked", "You haven't unlocked that item yet.");
    public static readonly Error NotForSale = new("Cosmetics.NotForSale", "That item can't be bought with Stars.");
    public static readonly Error InsufficientStars = new("Cosmetics.InsufficientStars", "You don't have enough available Stars.");
}

public sealed record CosmeticDto(
    Guid Id, string Kind, string Key, string NameEn, string NameAr, string? ImageUrl, string? AccentColor, string Unlock,
    int? StarsPrice, string? UnlockRankKey, bool Owned, bool Equipped);

public sealed record ProfileStyleDto(bool Enabled, IReadOnlyList<CosmeticDto> Items);

/// <summary>
/// Profile frames, badges and progress animations (EX-04). Rank, mission, ELITE and free unlocks are recomputed from the
/// customer's real state each time — so an ELITE-only frame stops showing the moment the subscription ends and returns on
/// renewal — while a Stars purchase is kept for good.
/// </summary>
public sealed class CosmeticService(
    ICommerceDbContext db, IPlatformSettingsProvider settings, LoyaltyRankService ranks, IMembershipAccessProvider membership, StarsLedgerService stars)
{
    public async Task<bool> IsEnabledAsync(CancellationToken cancellationToken) =>
        (await settings.GetAsync<FeatureCenterSettingsPayload>(PlatformSettingsCategoryKeys.Features, cancellationToken)).ProfileStyle;

    public async Task<ProfileStyleDto> GetAsync(string userId, CancellationToken cancellationToken)
    {
        if (!await IsEnabledAsync(cancellationToken))
        {
            return new ProfileStyleDto(false, []);
        }

        var all = await db.ProfileCosmetics.AsNoTracking().Where(c => c.IsActive).OrderBy(c => c.SortOrder).ToListAsync(cancellationToken);
        var owned = await OwnedIdsAsync(userId, all, cancellationToken);
        var style = await db.CustomerProfileStyles.AsNoTracking().FirstOrDefaultAsync(s => s.UserId == userId, cancellationToken);
        var worn = new[] { style?.FrameId, style?.BadgeId, style?.AnimationId }.Where(i => i.HasValue).Select(i => i!.Value).ToHashSet();

        return new ProfileStyleDto(true, all.Select(c => new CosmeticDto(
            c.Id, c.Kind.ToString(), c.Key, c.NameEn, c.NameAr, c.ImageUrl, c.AccentColor, c.Unlock.ToString(), c.StarsPrice, c.UnlockRankKey,
            owned.Contains(c.Id), owned.Contains(c.Id) && worn.Contains(c.Id))).ToList());
    }

    /// <summary>Wear the chosen items (each must be unlocked right now; null clears that slot).</summary>
    public async Task<Error?> EquipAsync(string userId, Guid? frameId, Guid? badgeId, Guid? animationId, CancellationToken cancellationToken)
    {
        if (!await IsEnabledAsync(cancellationToken))
        {
            return CosmeticErrors.Disabled;
        }

        var all = await db.ProfileCosmetics.AsNoTracking().Where(c => c.IsActive).ToListAsync(cancellationToken);
        var owned = await OwnedIdsAsync(userId, all, cancellationToken);
        var picks = new (Guid? Id, CosmeticKind Kind)[] { (frameId, CosmeticKind.Frame), (badgeId, CosmeticKind.Badge), (animationId, CosmeticKind.ProgressAnimation) };
        foreach (var (id, kind) in picks.Where(p => p.Id.HasValue))
        {
            var item = all.FirstOrDefault(c => c.Id == id);
            if (item is null || item.Kind != kind)
            {
                return CosmeticErrors.NotFound;
            }

            if (!owned.Contains(item.Id))
            {
                return CosmeticErrors.Locked;
            }
        }

        var row = await db.CustomerProfileStyles.FirstOrDefaultAsync(s => s.UserId == userId, cancellationToken);
        if (row is null)
        {
            row = CustomerProfileStyle.Create(userId);
            db.CustomerProfileStyles.Add(row);
        }

        row.Set(frameId, badgeId, animationId);
        await db.SaveChangesAsync(cancellationToken);
        return null;
    }

    public async Task<Error?> BuyAsync(string userId, Guid cosmeticId, CancellationToken cancellationToken)
    {
        if (!await IsEnabledAsync(cancellationToken))
        {
            return CosmeticErrors.Disabled;
        }

        var item = await db.ProfileCosmetics.AsNoTracking().FirstOrDefaultAsync(c => c.Id == cosmeticId && c.IsActive, cancellationToken);
        if (item is null)
        {
            return CosmeticErrors.NotFound;
        }

        if (item.Unlock != CosmeticUnlock.Stars || item.StarsPrice is not { } price)
        {
            return CosmeticErrors.NotForSale;
        }

        if (await db.CustomerCosmetics.AnyAsync(c => c.UserId == userId && c.CosmeticId == cosmeticId, cancellationToken))
        {
            return null;
        }

        if ((await stars.GetBalanceAsync(userId, cancellationToken)).Available < price)
        {
            return CosmeticErrors.InsufficientStars;
        }

        await stars.DebitAsync(userId, PointsTransactionType.Redeemed, price, $"Profile style: {item.NameEn}", $"cosmetic:{item.Id:N}:{userId}", item.Id.ToString(), cancellationToken);
        db.CustomerCosmetics.Add(CustomerCosmetic.Create(userId, item.Id));
        await db.SaveChangesAsync(cancellationToken);
        return null;
    }

    private async Task<HashSet<Guid>> OwnedIdsAsync(string userId, IReadOnlyList<ProfileCosmetic> all, CancellationToken cancellationToken)
    {
        var owned = (await db.CustomerCosmetics.AsNoTracking().Where(c => c.UserId == userId).Select(c => c.CosmeticId).ToListAsync(cancellationToken)).ToHashSet();

        var rankOrders = (await ranks.EnsureRanksAsync(cancellationToken)).ToDictionary(r => r.Key, r => r.SortOrder);
        var myRankKey = await db.CustomerLoyaltyRanks.AsNoTracking().Where(r => r.UserId == userId).Select(r => r.RankKey).FirstOrDefaultAsync(cancellationToken);
        var myRank = myRankKey is not null && rankOrders.TryGetValue(myRankKey, out var order) ? order : 0;
        var missions = (await db.MissionCompletions.AsNoTracking().Where(m => m.UserId == userId).Select(m => m.MissionId).ToListAsync(cancellationToken)).ToHashSet();
        var elite = (await membership.GetAccessInfoAsync(userId, cancellationToken)).SubscriptionId.HasValue;

        foreach (var c in all)
        {
            var unlocked = c.Unlock switch
            {
                CosmeticUnlock.Free => true,
                CosmeticUnlock.Rank => c.UnlockRankKey is not null && rankOrders.TryGetValue(c.UnlockRankKey, out var needed) && myRank >= needed,
                CosmeticUnlock.Mission => c.UnlockMissionId is { } m && missions.Contains(m),
                CosmeticUnlock.Elite => elite,
                _ => false,
            };
            if (unlocked)
            {
                owned.Add(c.Id);
            }
        }

        return owned;
    }
}
