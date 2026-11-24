using HAMBOX.Application.Abstractions;
using HAMBOX.Application.PlatformSettings;
using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Catalog.Application.Services;
using HAMBOX.Modules.Catalog.Domain.Enums;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Domain.Account;
using HAMBOX.Modules.Commerce.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Commerce.Application.Points;

/// <summary>Staff permission names for Stars; must equal <c>PermissionConstants.Stars.*</c> in Identity (a unit test asserts it).</summary>
public static class StarsPermissions
{
    public const string ApproveOverride = "Stars.ApproveOverride";
}

public sealed record StarsCampaignPreview(
    int AffectedProducts,
    IReadOnlyList<string> SampleProducts,
    decimal Last30DaysRevenueUsd,
    int EstimatedExtraStars,
    decimal EstimatedCostUsd,
    decimal? AverageMarginPercent,
    decimal? StarsCostPercent,
    decimal? MarginAfterStarsPercent,
    decimal MinMarginPercent,
    bool BelowMinimumMargin,
    bool CostsHidden);

/// <summary>
/// "Preview and report before it goes live" (CM-05) plus the margin guard (OP-02). The preview answers who is affected and
/// what the rule would have cost on the last 30 days of real sales; the guard refuses to publish a rule whose Stars cost
/// would push the products' margin below the configured floor unless a staff member holding Stars.ApproveOverride signs it off.
/// Cost-derived numbers are hidden from staff without the catalog's ViewCosts permission.
/// </summary>
public sealed class StarsCampaignService(
    ICommerceDbContext db, ICatalogDbContext catalog, IPlatformSettingsProvider settings, ICurrentUserPermissions permissions)
{
    public async Task<StarsCampaignPreview> PreviewAsync(
        StarsRuleScope scope, Guid? scopeId, StarsRuleMode mode, decimal value, CancellationToken cancellationToken)
    {
        var config = await settings.GetAsync<PurchasePointsSettingsPayload>(PlatformSettingsCategoryKeys.PurchasePoints, cancellationToken);
        var canSeeCosts = permissions.Has(CatalogPermissions.ViewCosts);

        var products = catalog.Products.AsNoTracking().Where(p => p.Status == ProductStatus.Active);
        products = scope switch
        {
            StarsRuleScope.Product => products.Where(p => p.Id == scopeId),
            StarsRuleScope.Category => products.Where(p => p.CategoryId == scopeId),
            _ => products,
        };

        var affected = await products.Select(p => new { p.Id, p.NameEn, p.Price }).ToListAsync(cancellationToken);
        var ids = affected.Select(p => p.Id).ToList();
        var since = DateTimeOffset.UtcNow.AddDays(-30);

        var revenue = ids.Count == 0
            ? 0m
            : await (from i in db.OrderItems.AsNoTracking()
                     join o in db.Orders.AsNoTracking() on i.OrderId equals o.Id
                     where i.ProductId != null && ids.Contains(i.ProductId.Value)
                         && o.Status == OrderStatus.Completed && o.PaymentStatus == PaymentStatus.Paid && o.CreatedOnUtc >= since
                     select (decimal?)i.LineTotal).SumAsync(cancellationToken) ?? 0m;

        var rate = config.PointsPerUsd;
        var extra = mode switch
        {
            StarsRuleMode.Multiplier => revenue * rate * Math.Max(value - 1m, 0m),
            StarsRuleMode.Exclude => -revenue * rate,
            _ => 0m,
        };
        var extraStars = (int)Math.Clamp(Math.Floor(extra), int.MinValue, int.MaxValue);
        var perStar = config.RedeemStarsPerUsd > 0m ? 1m / config.RedeemStarsPerUsd : 0m;

        decimal? avgMargin = null;
        decimal? starsCost = null;
        decimal? after = null;
        var below = false;

        if (canSeeCosts && ids.Count > 0)
        {
            var variants = await catalog.ProductVariants.AsNoTracking()
                .Where(v => ids.Contains(v.ProductId) && v.CostPrice != null)
                .Select(v => new { v.ProductId, v.PriceOverride, Cost = v.CostPrice!.Value })
                .ToListAsync(cancellationToken);
            var priceByProduct = affected.ToDictionary(p => p.Id, p => p.Price);

            var margins = new List<decimal>();
            var costs = new List<decimal>();
            foreach (var v in variants)
            {
                var price = v.PriceOverride ?? priceByProduct[v.ProductId];
                if (price <= 0m)
                {
                    continue;
                }

                margins.Add((price - v.Cost) / price * 100m);
                costs.Add(mode switch
                {
                    StarsRuleMode.Multiplier => rate * value * perStar * 100m,
                    StarsRuleMode.FixedPerUnit => value * perStar / price * 100m,
                    _ => 0m,
                });
            }

            if (margins.Count > 0)
            {
                avgMargin = Math.Round(margins.Average(), 2);
                starsCost = Math.Round(costs.Average(), 2);
                after = Math.Round(avgMargin.Value - starsCost.Value, 2);
                below = config.MinMarginPercent > 0m && after < config.MinMarginPercent;
            }
        }

        return new StarsCampaignPreview(
            affected.Count, affected.Take(20).Select(p => p.NameEn).ToList(), Math.Round(revenue, 2), extraStars,
            Math.Round(Math.Max(extraStars, 0) * perStar, 2), avgMargin, starsCost, after, config.MinMarginPercent, below, !canSeeCosts);
    }

    /// <summary>True when the rule may go live: it clears the margin floor, or the caller is allowed to approve an override.</summary>
    public async Task<(bool Allowed, StarsCampaignPreview Preview)> CheckAsync(
        StarsRuleScope scope, Guid? scopeId, StarsRuleMode mode, decimal value, bool overrideRequested, CancellationToken cancellationToken)
    {
        var preview = await PreviewAsync(scope, scopeId, mode, value, cancellationToken);
        return (!preview.BelowMinimumMargin || (overrideRequested && permissions.Has(StarsPermissions.ApproveOverride)), preview);
    }
}
