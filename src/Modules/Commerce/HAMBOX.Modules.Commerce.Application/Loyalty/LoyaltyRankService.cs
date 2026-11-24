using HAMBOX.Application.Abstractions;
using HAMBOX.Application.Communication;
using HAMBOX.Application.PlatformSettings;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Domain.Enums;
using HAMBOX.Modules.Commerce.Domain.Loyalty;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HAMBOX.Modules.Commerce.Application.Loyalty;

public sealed record LoyaltyRankDto(
    Guid Id, string Key, string NameEn, string NameAr, int SortOrder, decimal Threshold, bool IsVisible, string? AccentColor);

/// <param name="Metric">The customer's current qualifying value (USD spent or completed orders).</param>
public sealed record LoyaltyStatusDto(
    bool Enabled,
    string Criterion,
    LoyaltyRankDto Current,
    DateTimeOffset AchievedOnUtc,
    bool IsManual,
    LoyaltyRankDto? Next,
    decimal Metric,
    decimal Remaining,
    int ProgressPercent);

/// <summary>
/// Computes and stores customer loyalty ranks. The rank is driven by a qualifying metric (completed paid product
/// orders, in USD or by count) — never by the Stars balance, so spending Stars cannot lower it (LV-02). A staff-set rank
/// is pinned until cleared. Everything is a no-op while the Feature Center's LoyaltyRanks flag is off.
/// </summary>
public sealed class LoyaltyRankService(
    ICommerceDbContext db, IPlatformSettingsProvider settings, ILogger<LoyaltyRankService> logger, ICommunicationService? communication = null)
{
    private static readonly (string Key, string En, string Ar, decimal Threshold, string Color)[] Defaults =
    [
        (LoyaltyRankKeys.Normal, "Normal", "عادي", 0m, "#8892A6"),
        (LoyaltyRankKeys.Bronze, "Bronze", "نحاسي", 100m, "#B8733A"),
        (LoyaltyRankKeys.Silver, "Silver", "فضي", 500m, "#B7C0CE"),
        (LoyaltyRankKeys.Gold, "Gold", "ذهبي", 1500m, "#E0B13C"),
        (LoyaltyRankKeys.Platinum, "Platinum", "بلاتيني", 4000m, "#BFE3F2"),
        (LoyaltyRankKeys.Diamond, "Diamond", "كريستالي", 10000m, "#9B6CF0"),
    ];

    public async Task<bool> IsEnabledAsync(CancellationToken cancellationToken) =>
        (await settings.GetAsync<FeatureCenterSettingsPayload>(PlatformSettingsCategoryKeys.Features, cancellationToken)).LoyaltyRanks;

    /// <summary>Creates the six default rungs the first time the ladder is needed. Thresholds are placeholders for the admin to set.</summary>
    public async Task<IReadOnlyList<LoyaltyRank>> EnsureRanksAsync(CancellationToken cancellationToken)
    {
        var ranks = await db.LoyaltyRanks.ToListAsync(cancellationToken);
        if (ranks.Count > 0)
        {
            return ranks.OrderBy(r => r.SortOrder).ToList();
        }

        for (var i = 0; i < Defaults.Length; i++)
        {
            var d = Defaults[i];
            db.LoyaltyRanks.Add(LoyaltyRank.Create(d.Key, d.En, d.Ar, i, d.Threshold, d.Color));
        }

        await db.SaveChangesAsync(cancellationToken);
        return await db.LoyaltyRanks.OrderBy(r => r.SortOrder).ToListAsync(cancellationToken);
    }

    public static LoyaltyRankDto ToDto(LoyaltyRank r) =>
        new(r.Id, r.Key, r.NameEn, r.NameAr, r.SortOrder, r.Threshold, r.IsVisible, r.AccentColor);

    public async Task<decimal> GetMetricAsync(string userId, string criterion, CancellationToken cancellationToken)
    {
        var orders = db.Orders.AsNoTracking().Where(o =>
            o.UserId == userId && o.Kind == OrderKind.Product && o.Status == OrderStatus.Completed && o.PaymentStatus == PaymentStatus.Paid);

        return criterion == LoyaltyCriterion.CompletedOrders
            ? await orders.CountAsync(cancellationToken)
            : await orders.SumAsync(o => (decimal?)o.TotalAmount, cancellationToken) ?? 0m;
    }

    /// <summary>Re-evaluates one customer. Best-effort: runs inside order completion/refund flows, so it never throws.</summary>
    public async Task EvaluateAsync(string userId, CancellationToken cancellationToken)
    {
        try
        {
            if (await IsEnabledAsync(cancellationToken))
            {
                await ApplyAsync(userId, cancellationToken);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Could not evaluate the loyalty rank for user {UserId}.", userId);
        }
    }

    /// <summary>Re-evaluates every customer who has a completed order (used when the feature is first switched on).</summary>
    public async Task<int> RecalculateAllAsync(CancellationToken cancellationToken)
    {
        var userIds = await db.Orders.AsNoTracking()
            .Where(o => o.Kind == OrderKind.Product && o.Status == OrderStatus.Completed)
            .Select(o => o.UserId).Distinct().ToListAsync(cancellationToken);

        foreach (var userId in userIds)
        {
            await ApplyAsync(userId, cancellationToken);
        }

        return userIds.Count;
    }

    public async Task<LoyaltyStatusDto> GetStatusAsync(string userId, CancellationToken cancellationToken)
    {
        var enabled = await IsEnabledAsync(cancellationToken);
        if (enabled)
        {
            await ApplyAsync(userId, cancellationToken);
        }

        var ranks = (await EnsureRanksAsync(cancellationToken)).Where(r => r.IsVisible).OrderBy(r => r.SortOrder).ToList();
        var config = await settings.GetAsync<LoyaltySettingsPayload>(PlatformSettingsCategoryKeys.Loyalty, cancellationToken);
        var row = await db.CustomerLoyaltyRanks.AsNoTracking().FirstOrDefaultAsync(r => r.UserId == userId, cancellationToken);
        var current = ranks.FirstOrDefault(r => r.Key == (row?.RankKey ?? LoyaltyRankKeys.Normal)) ?? ranks[0];
        var metric = await GetMetricAsync(userId, config.Criterion, cancellationToken);
        var next = ranks.FirstOrDefault(r => r.SortOrder > current.SortOrder && r.Threshold > current.Threshold);

        var percent = next is null
            ? 100
            : (int)Math.Clamp((metric - current.Threshold) * 100m / Math.Max(next.Threshold - current.Threshold, 1m), 0m, 100m);

        return new LoyaltyStatusDto(
            enabled, config.Criterion, ToDto(current), row?.AchievedOnUtc ?? DateTimeOffset.MinValue, row?.IsManual ?? false,
            next is null ? null : ToDto(next), metric, next is null ? 0m : Math.Max(next.Threshold - metric, 0m), percent);
    }

    /// <summary>Pins a rank chosen by staff (with a reason) or, with <paramref name="rankKey"/> null, releases the pin and re-evaluates.</summary>
    public async Task<bool> SetManualAsync(
        string userId, string? rankKey, string reason, string actorUserId, CancellationToken cancellationToken)
    {
        var ranks = await EnsureRanksAsync(cancellationToken);
        if (rankKey is not null && ranks.All(r => r.Key != rankKey))
        {
            return false;
        }

        var row = await GetOrCreateRowAsync(userId, cancellationToken);
        if (rankKey is null)
        {
            row.MoveTo(row.RankKey, isManual: false);
            await db.SaveChangesAsync(cancellationToken);
            await ApplyAsync(userId, cancellationToken, ignoreDowngradePolicy: true);
            return true;
        }

        Move(row, rankKey, true, reason, actorUserId);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task ApplyAsync(string userId, CancellationToken cancellationToken, bool ignoreDowngradePolicy = false)
    {
        var ranks = (await EnsureRanksAsync(cancellationToken)).Where(r => r.IsVisible).OrderBy(r => r.SortOrder).ToList();
        var config = await settings.GetAsync<LoyaltySettingsPayload>(PlatformSettingsCategoryKeys.Loyalty, cancellationToken);
        var row = await GetOrCreateRowAsync(userId, cancellationToken);
        if (row.IsManual)
        {
            return;
        }

        var metric = await GetMetricAsync(userId, config.Criterion, cancellationToken);
        var earned = ranks.LastOrDefault(r => r.Threshold <= metric) ?? ranks[0];
        var currentOrder = ranks.FirstOrDefault(r => r.Key == row.RankKey)?.SortOrder ?? 0;

        if (earned.Key == row.RankKey || (!config.AllowDowngrade && !ignoreDowngradePolicy && earned.SortOrder < currentOrder))
        {
            return;
        }

        var promoted = earned.SortOrder > currentOrder;
        Move(row, earned.Key, false, promoted ? "Reached the rank threshold." : "Qualifying activity fell below the threshold.", null);
        await db.SaveChangesAsync(cancellationToken);

        if (promoted && communication is not null)
        {
            await communication.SendAsync(new CommunicationRequest(
                UserId: userId,
                TemplateKey: "RankUp",
                Category: CommunicationCategory.Promotion,
                Variables: new Dictionary<string, string> { ["Rank"] = earned.NameEn, ["RankAr"] = earned.NameAr },
                RelatedEntityType: "LoyaltyRank",
                RelatedEntityId: earned.Key,
                ActionUrl: "/account/points"), cancellationToken);
        }
    }

    private async Task<CustomerLoyaltyRank> GetOrCreateRowAsync(string userId, CancellationToken cancellationToken)
    {
        var row = await db.CustomerLoyaltyRanks.FirstOrDefaultAsync(r => r.UserId == userId, cancellationToken);
        if (row is null)
        {
            row = CustomerLoyaltyRank.Create(userId);
            db.CustomerLoyaltyRanks.Add(row);
        }

        return row;
    }

    private void Move(CustomerLoyaltyRank row, string toKey, bool manual, string reason, string? actor)
    {
        var from = row.RankKey;
        row.MoveTo(toKey, manual);
        db.LoyaltyRankHistory.Add(LoyaltyRankHistory.Record(row.UserId, from, toKey, reason, actor));
    }
}
