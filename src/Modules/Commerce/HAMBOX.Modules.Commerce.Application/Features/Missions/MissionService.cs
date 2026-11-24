using HAMBOX.Application.PlatformSettings;
using HAMBOX.Application.Abstractions;
using HAMBOX.Application.Communication;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Points;
using HAMBOX.Modules.Commerce.Domain.Account;
using HAMBOX.Modules.Commerce.Domain.Enums;
using HAMBOX.Modules.Commerce.Domain.Missions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HAMBOX.Modules.Commerce.Application.Features.Missions;

public sealed record MissionProgressDto(
    Guid Id, string NameEn, string NameAr, string? DescriptionEn, string? DescriptionAr, string Kind, decimal Target, decimal Current,
    int RewardStars, string? BadgeKey, bool Completed, DateTimeOffset? CompletedOnUtc);

/// <summary>
/// Measures customers against the live missions and pays each one out once. Best-effort at the order-completion seam (it
/// must never break a delivery); the unique (mission, customer) row and the idempotent Stars key make a repeat a no-op.
/// </summary>
public sealed class MissionService(
    ICommerceDbContext db, IPlatformSettingsProvider settings, StarsLedgerService stars, ILogger<MissionService> logger,
    ICommunicationService? communication = null)
{
    public async Task<bool> IsEnabledAsync(CancellationToken cancellationToken) =>
        (await settings.GetAsync<FeatureCenterSettingsPayload>(PlatformSettingsCategoryKeys.Features, cancellationToken)).Missions;

    public async Task EvaluateAsync(string userId, CancellationToken cancellationToken)
    {
        try
        {
            if (await IsEnabledAsync(cancellationToken))
            {
                await CompleteDueAsync(userId, cancellationToken);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Could not evaluate missions for user {UserId}.", userId);
        }
    }

    public async Task<IReadOnlyList<MissionProgressDto>> GetProgressAsync(string userId, CancellationToken cancellationToken)
    {
        if (!await IsEnabledAsync(cancellationToken))
        {
            return [];
        }

        await CompleteDueAsync(userId, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var missions = (await db.Missions.AsNoTracking().Where(m => m.IsActive).OrderBy(m => m.SortOrder).ToListAsync(cancellationToken))
            .Where(m => m.IsLiveAt(now)).ToList();
        var done = await db.MissionCompletions.AsNoTracking().Where(c => c.UserId == userId).ToDictionaryAsync(c => c.MissionId, cancellationToken);
        var (orders, spend) = await MetricsAsync(userId, cancellationToken);

        return missions.Select(m =>
        {
            done.TryGetValue(m.Id, out var completion);
            var current = m.Kind == MissionKind.CompletedOrders ? orders : spend;
            return new MissionProgressDto(
                m.Id, m.NameEn, m.NameAr, m.DescriptionEn, m.DescriptionAr, m.Kind.ToString(), m.Target, Math.Min(current, m.Target),
                m.RewardStars, m.BadgeKey, completion is not null, completion?.CompletedOnUtc);
        }).ToList();
    }

    private async Task CompleteDueAsync(string userId, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var missions = (await db.Missions.AsNoTracking().Where(m => m.IsActive).ToListAsync(cancellationToken)).Where(m => m.IsLiveAt(now)).ToList();
        if (missions.Count == 0)
        {
            return;
        }

        var done = (await db.MissionCompletions.AsNoTracking().Where(c => c.UserId == userId).Select(c => c.MissionId).ToListAsync(cancellationToken)).ToHashSet();
        var (orders, spend) = await MetricsAsync(userId, cancellationToken);

        foreach (var mission in missions.Where(m => !done.Contains(m.Id)))
        {
            var current = mission.Kind == MissionKind.CompletedOrders ? orders : spend;
            if (current < mission.Target)
            {
                continue;
            }

            db.MissionCompletions.Add(MissionCompletion.Create(mission.Id, userId, mission.RewardStars));
            try
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                // Completed concurrently by another request: the unique (mission, customer) index kept it to one.
                continue;
            }

            if (mission.RewardStars > 0)
            {
                await stars.CreditAsync(
                    userId, PointsTransactionType.Bonus, mission.RewardStars, $"Mission completed: {mission.NameEn}.",
                    $"mission:{mission.Id:N}:{userId}", reference: mission.Id.ToString(), cancellationToken: cancellationToken);
            }

            if (communication is not null)
            {
                await communication.SendAsync(new CommunicationRequest(
                    UserId: userId,
                    TemplateKey: "MissionCompleted",
                    Category: CommunicationCategory.Promotion,
                    Variables: new Dictionary<string, string> { ["Mission"] = mission.NameEn, ["MissionAr"] = mission.NameAr, ["Stars"] = mission.RewardStars.ToString() },
                    RelatedEntityType: "Mission",
                    RelatedEntityId: mission.Id.ToString(),
                    ActionUrl: "/account/rewards"), cancellationToken);
            }
        }
    }

    private async Task<(decimal Orders, decimal Spend)> MetricsAsync(string userId, CancellationToken cancellationToken)
    {
        var delivered = db.Orders.AsNoTracking().Where(o =>
            o.UserId == userId && o.Kind == OrderKind.Product && o.Status == OrderStatus.Completed && o.PaymentStatus == PaymentStatus.Paid);
        return (await delivered.CountAsync(cancellationToken), await delivered.SumAsync(o => (decimal?)o.TotalAmount, cancellationToken) ?? 0m);
    }
}
