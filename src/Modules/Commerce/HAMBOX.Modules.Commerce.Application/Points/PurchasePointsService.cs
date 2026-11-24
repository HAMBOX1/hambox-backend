using HAMBOX.Application.Abstractions;
using HAMBOX.Application.PlatformSettings;
using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Memberships;
using HAMBOX.Modules.Commerce.Domain.Account;
using HAMBOX.Modules.Commerce.Domain.Memberships;
using HAMBOX.Modules.Commerce.Domain.Enums;
using HAMBOX.Modules.Commerce.Domain.Orders;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HAMBOX.Modules.Commerce.Application.Points;

/// <summary>
/// Awards and reverses purchase points against the append-only <see cref="PointsTransaction"/> ledger.
/// Both operations are idempotent (the ledger's unique key rejects a second row for the same effect) and are
/// deliberately best-effort: they run inside order completion / refund flows, so a failure here is logged and
/// swallowed rather than allowed to roll back a payment confirmation. Amounts are USD — the canonical stored
/// price currency — so display currency never affects points.
/// </summary>
public sealed class PurchasePointsService(
    ICommerceDbContext db,
    IPlatformSettingsProvider platformSettings,
    ILogger<PurchasePointsService> logger,
    ICatalogDbContext? catalog = null,
    IMembershipEngine? membershipEngine = null,
    IUserDirectory? users = null)
{
    /// <summary>
    /// Credits points for a paid, completed order at the rate configured right now. A no-op when the program is
    /// off, the order isn't paid, it has already been credited, or the amount rounds down to zero points.
    /// </summary>
    public async Task AwardForOrderAsync(Order order, CancellationToken cancellationToken)
    {
        try
        {
            var settings = await platformSettings.GetAsync<PurchasePointsSettingsPayload>(
                PlatformSettingsCategoryKeys.PurchasePoints, cancellationToken);

            if (!settings.Enabled || settings.PointsPerUsd <= 0m || !IsEligible(order))
            {
                return;
            }

            // B2B-04: a wholesale order does not earn Stars unless its business account was made eligible.
            if (order.PaymentMethod == "b2b"
                && !await db.BusinessAccounts.AsNoTracking().AnyAsync(a => a.UserId == order.UserId && a.StarsEligible, cancellationToken))
            {
                return;
            }

            var eligibleAmount = EligibleAmountUsd(order, settings.AmountBasis);
            var now = DateTimeOffset.UtcNow;
            var rules = await db.StarsEarnRules.AsNoTracking().Where(r => r.IsActive).ToListAsync(cancellationToken);
            var elite = await ResolveEliteBonusAsync(order.UserId, cancellationToken);
            var (audience, rankOrders) = await ResolveAudienceAsync(order.UserId, elite.IsElite, rules.Count > 0, cancellationToken);
            var lines = await BuildLinesAsync(order, rules.Count > 0, cancellationToken);
            var context = rules.Any(r => r.Condition != StarsRuleCondition.None) ? await BuildContextAsync(order, eligibleAmount, now, cancellationToken) : null;
            var limits = await BuildLimitsAsync(order.UserId, rules, cancellationToken);
            var result = StarsEarnCalculator.CalculateDetailed(
                lines, eligibleAmount, settings.PointsPerUsd, rules, now, audience, rankOrders, context, limits,
                settings.StackingMode, settings.MaxMultiplier);
            var points = elite.Apply(result.Total);
            if (points <= 0)
            {
                return;
            }

            var key = PointsTransaction.EarnKey(order.Id);
            if (await db.PointsTransactions.AnyAsync(t => t.IdempotencyKey == key, cancellationToken))
            {
                return;
            }

            // ST-04: delivery is complete (this runs on completion); a configured hold keeps the credit Pending until it passes.
            var held = settings.HoldDays > 0;
            db.PointsTransactions.Add(PointsTransaction.Earn(
                order.UserId, order.Id, order.OrderNumber, points, eligibleAmount, settings.PointsPerUsd,
                held ? StarsStatus.Pending : StarsStatus.Available,
                held ? now.AddDays(settings.HoldDays) : null,
                settings.ExpiryDays > 0 ? now.AddDays(settings.HoldDays + settings.ExpiryDays) : null));
            foreach (var (ruleId, stars) in result.PerRule.Where(p => p.Value > 0))
            {
                db.StarsRuleAwards.Add(StarsRuleAward.Create(ruleId, order.UserId, order.Id, stars));
            }

            await db.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "Awarded {Points} purchase points to user {UserId} for order {OrderNumber}.",
                points, order.UserId, order.OrderNumber);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // A concurrent duplicate lands here via the unique key (DbUpdateException); anything else is a real fault.
            // ponytail: no retry/backfill job — an order whose award failed stays un-credited until re-completed or backfilled by hand.
            logger.LogError(ex, "Could not award purchase points for order {OrderNumber}.", order.OrderNumber);
        }
    }

    /// <summary>
    /// Takes back the points earned on an order. With no <paramref name="refundedAmountUsd"/> (cancellation or a
    /// full refund) everything still outstanding is reversed; with an amount, the share matching the refunded
    /// fraction of the order total is — cumulatively, so repeated or overlapping partial refunds never take back more
    /// than was earned. Always uses the points originally awarded, never today's rate.
    /// </summary>
    public async Task ReverseForOrderAsync(Order order, decimal? refundedAmountUsd, CancellationToken cancellationToken)
    {
        try
        {
            var earnRow = await db.PointsTransactions
                .FirstOrDefaultAsync(t => t.OrderId == order.Id && t.Type == PointsTransactionType.Earned, cancellationToken);
            var earned = earnRow?.Points ?? 0;

            if (earned <= 0)
            {
                return;
            }

            // Never spendable yet, so there is nothing to claw back: void the credit instead of adding a negative row.
            // ponytail: a partial refund voids the whole pending credit; split it if clients object.
            if (earnRow!.Status == StarsStatus.Pending)
            {
                earnRow.Cancel();
                await db.SaveChangesAsync(cancellationToken);
                return;
            }

            var alreadyReversed = -await db.PointsTransactions
                .Where(t => t.OrderId == order.Id && t.Type == PointsTransactionType.Reversed)
                .SumAsync(t => (int?)t.Points, cancellationToken) ?? 0;

            var target = refundedAmountUsd is { } refunded && order.TotalAmount > 0m && refunded < order.TotalAmount
                ? (int)Math.Min(earned, Math.Round(earned * Math.Max(refunded, 0m) / order.TotalAmount, MidpointRounding.AwayFromZero))
                : earned;

            var delta = target - alreadyReversed;
            if (delta <= 0)
            {
                return;
            }

            var reason = refundedAmountUsd is { } amount && target < earned
                ? $"Order {order.OrderNumber} was partially refunded ({amount:0.00} USD)."
                : $"Order {order.OrderNumber} was cancelled or refunded.";

            db.PointsTransactions.Add(PointsTransaction.Reverse(
                order.UserId, order.Id, order.OrderNumber, delta, target, reason));

            // A fully reversed order gives its campaign budget back.
            if (target >= earned)
            {
                db.StarsRuleAwards.RemoveRange(await db.StarsRuleAwards.Where(a => a.OrderId == order.Id).ToListAsync(cancellationToken));
            }

            await db.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "Reversed {Points} purchase points for order {OrderNumber} (user {UserId}).",
                delta, order.OrderNumber, order.UserId);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Could not reverse purchase points for order {OrderNumber}.", order.OrderNumber);
        }
    }

    private async Task<StarsOrderContext> BuildContextAsync(Order order, decimal eligibleAmount, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var prior = db.Orders.AsNoTracking().Where(o =>
            o.UserId == order.UserId && o.Id != order.Id && o.Kind == OrderKind.Product && o.Status == OrderStatus.Completed && o.PaymentStatus == PaymentStatus.Paid);
        var count = await prior.CountAsync(cancellationToken);
        var last = count == 0 ? null : await prior.MaxAsync(o => (DateTimeOffset?)o.CreatedOnUtc, cancellationToken);

        var anniversary = false;
        if (users is not null && await users.GetRegisteredOnAsync(order.UserId, cancellationToken) is { } registered)
        {
            var thisYear = new DateTimeOffset(now.Year, Math.Min(registered.Month, 12), Math.Min(registered.Day, DateTime.DaysInMonth(now.Year, registered.Month)), 0, 0, 0, TimeSpan.Zero);
            anniversary = now.Year > registered.Year && Math.Abs((now - thisYear).TotalDays) <= 3;
        }

        return new StarsOrderContext(
            eligibleAmount,
            order.Items.Sum(i => i.Quantity),
            count,
            last is { } l ? (int)(now - l).TotalDays : null,
            anniversary,
            order.Items.Where(i => i.ProductId.HasValue).Select(i => i.ProductId!.Value).ToHashSet());
    }

    private async Task<IReadOnlyDictionary<Guid, StarsRuleLimit>> BuildLimitsAsync(
        string userId, IReadOnlyList<StarsEarnRule> rules, CancellationToken cancellationToken)
    {
        var limited = rules.Where(r => r.BudgetStars is not null || r.MaxStarsPerCustomer is not null).ToList();
        if (limited.Count == 0)
        {
            return new Dictionary<Guid, StarsRuleLimit>();
        }

        var ids = limited.Select(r => r.Id).ToList();
        var campaign = await db.StarsRuleAwards.AsNoTracking().Where(a => ids.Contains(a.RuleId))
            .GroupBy(a => a.RuleId).Select(g => new { g.Key, Sum = g.Sum(a => a.Stars) }).ToDictionaryAsync(x => x.Key, x => x.Sum, cancellationToken);
        var customer = await db.StarsRuleAwards.AsNoTracking().Where(a => ids.Contains(a.RuleId) && a.UserId == userId)
            .GroupBy(a => a.RuleId).Select(g => new { g.Key, Sum = g.Sum(a => a.Stars) }).ToDictionaryAsync(x => x.Key, x => x.Sum, cancellationToken);

        return limited.ToDictionary(
            r => r.Id,
            r => new StarsRuleLimit(
                r.BudgetStars is { } b ? Math.Max(b - campaign.GetValueOrDefault(r.Id), 0) : null,
                r.MaxStarsPerCustomer is { } c ? Math.Max(c - customer.GetValueOrDefault(r.Id), 0) : null));
    }

    private readonly record struct EliteBonus(bool IsElite, decimal Percent)
    {
        public int Apply(int points) =>
            Percent > 0m && points > 0 ? (int)Math.Min(Math.Floor(points * (1m + Percent / 100m)), int.MaxValue) : points;
    }

    /// <summary>ELITE membership state: whether the customer holds an active subscription, and the plan's "Bonus Stars" percentage.</summary>
    private async Task<EliteBonus> ResolveEliteBonusAsync(string userId, CancellationToken cancellationToken)
    {
        if (membershipEngine is null)
        {
            return default;
        }

        var features = await platformSettings.GetAsync<FeatureCenterSettingsPayload>(PlatformSettingsCategoryKeys.Features, cancellationToken);
        if (!features.EliteSubscription)
        {
            return default;
        }

        var snapshot = await membershipEngine.ResolveAsync(userId, cancellationToken);
        if (snapshot.SubscriptionId == Guid.Empty)
        {
            return default;
        }

        var bonus = snapshot.Benefits.FirstOrDefault(b => b.Type.Equals(nameof(MembershipBenefitType.BonusStars), StringComparison.OrdinalIgnoreCase))?.Value;
        return new EliteBonus(
            true,
            decimal.TryParse(bonus, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var percent) ? percent : 0m);
    }

    private async Task<(StarsAudience Audience, IReadOnlyDictionary<string, int>? RankOrders)> ResolveAudienceAsync(
        string userId, bool isElite, bool anyRules, CancellationToken cancellationToken)
    {
        if (!anyRules)
        {
            return (new StarsAudience(0, isElite), null);
        }

        var orders = await db.LoyaltyRanks.AsNoTracking().ToDictionaryAsync(r => r.Key, r => r.SortOrder, cancellationToken);
        var key = await db.CustomerLoyaltyRanks.AsNoTracking().Where(r => r.UserId == userId).Select(r => r.RankKey).FirstOrDefaultAsync(cancellationToken);
        return (new StarsAudience(key is not null && orders.TryGetValue(key, out var order) ? order : 0, isElite), orders);
    }

    private async Task<List<StarsEarnLine>> BuildLinesAsync(Order order, bool needCategories, CancellationToken cancellationToken)
    {
        var categories = new Dictionary<Guid, Guid>();
        if (needCategories && catalog is not null)
        {
            var ids = order.Items.Where(i => i.ProductId.HasValue).Select(i => i.ProductId!.Value).Distinct().ToList();
            categories = await catalog.Products.AsNoTracking()
                .Where(p => ids.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id, p => p.CategoryId, cancellationToken);
        }

        return order.Items
            .Select(i => new StarsEarnLine(
                i.ProductId,
                i.ProductId is { } id && categories.TryGetValue(id, out var cat) ? cat : null,
                i.Quantity,
                i.LineTotal))
            .ToList();
    }

    // Paid and still live. Completed implies paid, but the payment check keeps this safe if a caller ever passes
    // an order from another path; Failed/Cancelled/Refunded/Pending never earn.
    private static bool IsEligible(Order order) =>
        order.PaymentStatus == PaymentStatus.Paid
        && order.Status is OrderStatus.Completed or OrderStatus.Processing;

    private static decimal EligibleAmountUsd(Order order, string amountBasis)
    {
        var amount = amountBasis == PurchasePointsAmountBasis.Total
            ? order.TotalAmount
            : order.Subtotal - order.DiscountAmount;

        return Math.Max(amount, 0m);
    }
}
