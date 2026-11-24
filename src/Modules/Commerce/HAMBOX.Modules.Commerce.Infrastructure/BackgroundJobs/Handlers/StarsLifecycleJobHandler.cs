using System.Globalization;
using HAMBOX.Application.BackgroundJobs;
using HAMBOX.Application.Communication;
using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Domain.Account;
using HAMBOX.Modules.Commerce.Domain.Operations;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Commerce.Infrastructure.BackgroundJobs.Handlers;

/// <summary>
/// Hourly Stars notices (EX-02): tells customers once when Stars they hold are about to expire, and tells the customers who
/// follow a product (it is on their wishlist) once when a bonus campaign for it starts. Campaign notices go out as marketing
/// messages, so customers who opted out of marketing never receive them; the expiry notice is operational.
/// </summary>
internal sealed class StarsLifecycleJobHandler(
    IBackgroundJobSerializer serializer,
    ICommerceDbContext db,
    ICatalogDbContext catalog,
    ICommunicationService communication) : BackgroundJobHandlerBase<string?>(serializer)
{
    private static readonly TimeSpan ExpiryLead = TimeSpan.FromDays(7);

    public override string JobType => OperationalJobTypes.ProcessStarsLifecycle;

    public override async Task HandleAsync(string? payload, IBackgroundJobContext context, CancellationToken cancellationToken)
    {
        await NotifyExpiringAsync(cancellationToken);
        await AnnounceCampaignsAsync(cancellationToken);
    }

    private async Task NotifyExpiringAsync(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var due = await db.PointsTransactions
            .Where(t => t.Status == StarsStatus.Available && t.Points > 0 && t.ExpiresOnUtc != null
                && t.ExpiresOnUtc > now && t.ExpiresOnUtc <= now.Add(ExpiryLead) && t.ExpiryNotifiedOnUtc == null)
            .ToListAsync(cancellationToken);

        foreach (var group in due.GroupBy(t => t.UserId))
        {
            var earliest = group.Min(t => t.ExpiresOnUtc!.Value);
            foreach (var row in group)
            {
                row.MarkExpiryNotified();
            }

            await db.SaveChangesAsync(cancellationToken);
            await communication.SendAsync(new CommunicationRequest(
                UserId: group.Key,
                TemplateKey: "StarsExpiringSoon",
                Category: CommunicationCategory.System,
                Variables: new Dictionary<string, string>
                {
                    ["Stars"] = group.Sum(t => t.Points).ToString(CultureInfo.InvariantCulture),
                    ["Date"] = earliest.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                },
                RelatedEntityType: "Stars",
                RelatedEntityId: group.Key,
                ActionUrl: "/account/rewards"), cancellationToken);
        }
    }

    private async Task AnnounceCampaignsAsync(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var rules = await db.StarsEarnRules
            .Where(r => r.IsActive && r.AnnouncedOnUtc == null && r.StartsOnUtc != null && r.StartsOnUtc <= now
                && (r.EndsOnUtc == null || r.EndsOnUtc > now) && r.Scope != StarsRuleScope.Store
                && (r.Mode == StarsRuleMode.Multiplier || r.Mode == StarsRuleMode.FixedPerUnit || r.Mode == StarsRuleMode.FlatBonus))
            .ToListAsync(cancellationToken);

        foreach (var rule in rules)
        {
            var productIds = rule.Scope == StarsRuleScope.Product
                ? [rule.ScopeId!.Value]
                : await catalog.Products.AsNoTracking().Where(p => p.CategoryId == rule.ScopeId).Select(p => p.Id).Take(2000).ToListAsync(cancellationToken);

            var followers = await db.WishlistItems.AsNoTracking()
                .Where(w => productIds.Contains(w.ProductId)).Select(w => w.UserId).Distinct().Take(5000).ToListAsync(cancellationToken);

            rule.MarkAnnounced();
            await db.SaveChangesAsync(cancellationToken);

            foreach (var userId in followers)
            {
                await communication.SendAsync(new CommunicationRequest(
                    UserId: userId,
                    TemplateKey: "StarsCampaignStarted",
                    Category: CommunicationCategory.Marketing,
                    Variables: new Dictionary<string, string>
                    {
                        ["Campaign"] = rule.Name,
                        ["Offer"] = rule.Mode == StarsRuleMode.Multiplier ? $"x{rule.Value.ToString("0.##", CultureInfo.InvariantCulture)}" : $"+{rule.Value.ToString("0", CultureInfo.InvariantCulture)}",
                    },
                    RelatedEntityType: "StarsEarnRule",
                    RelatedEntityId: rule.Id.ToString(),
                    ActionUrl: "/account/wishlist"), cancellationToken);
            }
        }
    }
}
