using HAMBOX.Application.PlatformSettings;
using HAMBOX.Modules.Commerce.Domain.Account;

namespace HAMBOX.Modules.Commerce.Application.Points;

/// <summary>One order line as the earn calculator sees it.</summary>
public readonly record struct StarsEarnLine(Guid? ProductId, Guid? CategoryId, int Quantity, decimal LineTotal);

/// <summary>Who is earning: their loyalty rung (by sort order) and whether they hold an active ELITE subscription.</summary>
public readonly record struct StarsAudience(int RankOrder, bool IsElite)
{
    public static StarsAudience Anyone => new(0, false);
}

/// <summary>What is known about the order and its buyer for campaign conditions (first order, comeback, bundle…).</summary>
public sealed record StarsOrderContext(
    decimal EligibleAmountUsd,
    int ItemCount,
    int PriorDeliveredOrders,
    int? DaysSinceLastOrder,
    bool AnniversaryActive,
    IReadOnlySet<Guid> ProductIds)
{
    public static StarsOrderContext Empty { get; } = new(0m, 0, 0, null, false, new HashSet<Guid>());
}

/// <summary>How much more bonus a rule may still pay: the campaign's remaining budget and this customer's remaining allowance.</summary>
public readonly record struct StarsRuleLimit(int? CampaignRemaining, int? CustomerRemaining)
{
    public decimal Allowance => Math.Min(CampaignRemaining ?? decimal.MaxValue, CustomerRemaining ?? decimal.MaxValue);
}

/// <param name="Total">Stars for the whole order before the ELITE bonus.</param>
/// <param name="PerRule">The bonus (above the base rate) each rule contributed — what its budget is charged.</param>
public sealed record StarsEarnResult(int Total, IReadOnlyDictionary<Guid, int> PerRule);

/// <summary>
/// Pure Stars arithmetic. The order's eligible USD amount (already net of discounts/tax per the configured basis)
/// is shared across lines in proportion to their line totals, each line is resolved against the live
/// <see cref="StarsEarnRule"/>s, and the unrounded line results are summed before a single floor — so with no
/// matching rules the answer is exactly the legacy <c>floor(amount × rate)</c>. Rules apply only when their audience and
/// condition match; how overlapping rules combine is the stacking mode; budgets cap what a rule may pay.
/// </summary>
public static class StarsEarnCalculator
{
    public static int Calculate(
        IReadOnlyList<StarsEarnLine> lines,
        decimal eligibleAmountUsd,
        decimal baseRatePerUsd,
        IReadOnlyList<StarsEarnRule> rules,
        DateTimeOffset now,
        StarsAudience audience = default,
        IReadOnlyDictionary<string, int>? rankOrders = null) =>
        CalculateDetailed(lines, eligibleAmountUsd, baseRatePerUsd, rules, now, audience, rankOrders).Total;

    public static StarsEarnResult CalculateDetailed(
        IReadOnlyList<StarsEarnLine> lines,
        decimal eligibleAmountUsd,
        decimal baseRatePerUsd,
        IReadOnlyList<StarsEarnRule> rules,
        DateTimeOffset now,
        StarsAudience audience = default,
        IReadOnlyDictionary<string, int>? rankOrders = null,
        StarsOrderContext? context = null,
        IReadOnlyDictionary<Guid, StarsRuleLimit>? limits = null,
        string stackingMode = StarsStackingMode.HighestOffer,
        decimal maxMultiplier = 0m)
    {
        var ctx = context ?? StarsOrderContext.Empty;
        var live = rules.Where(r => r.IsLiveAt(now) && Matches(r, audience, rankOrders, context)).ToList();
        var lineTotal = lines.Sum(l => l.LineTotal);
        var perRule = new Dictionary<Guid, decimal>();

        if (live.Count == 0 || lines.Count == 0 || lineTotal <= 0m)
        {
            return new StarsEarnResult(ToStars(eligibleAmountUsd * baseRatePerUsd), new Dictionary<Guid, int>());
        }

        var combine = stackingMode == StarsStackingMode.Combine;
        var perOrderUsed = new Dictionary<Guid, decimal>();
        var total = 0m;

        // The remaining allowance of a rule's budget, shrinking as lines draw on it.
        decimal Allowance(StarsEarnRule rule) =>
            (limits is not null && limits.TryGetValue(rule.Id, out var l) ? l.Allowance : decimal.MaxValue) - perRule.GetValueOrDefault(rule.Id);

        foreach (var line in lines)
        {
            var share = eligibleAmountUsd * line.LineTotal / lineTotal;
            var baseline = share * baseRatePerUsd;
            var matching = Matching(line, live.Where(r => r.Mode != StarsRuleMode.FlatBonus)).ToList();

            decimal earned;
            if (matching.Count == 0)
            {
                earned = baseline;
            }
            else if (combine)
            {
                if (matching.Any(r => r.Mode == StarsRuleMode.Exclude))
                {
                    earned = 0m;
                }
                else
                {
                    var multiplier = 1m + matching.Where(r => r.Mode == StarsRuleMode.Multiplier).Sum(r => r.Value - 1m);
                    if (maxMultiplier > 0m)
                    {
                        multiplier = Math.Min(multiplier, maxMultiplier);
                    }

                    earned = baseline * Math.Max(multiplier, 0m);
                    earned += matching.Where(r => r.Mode == StarsRuleMode.FixedPerUnit).Sum(r => Math.Floor(r.Value) * line.Quantity);
                }
            }
            else
            {
                var winner = Resolve(line, matching)!;
                earned = winner.Mode switch
                {
                    StarsRuleMode.Exclude => 0m,
                    StarsRuleMode.FixedPerUnit => Math.Floor(winner.Value) * line.Quantity,
                    StarsRuleMode.Multiplier => baseline * winner.Value,
                    _ => baseline,
                };
                matching = [winner];
            }

            // Budgets (campaign total and per customer) cap only the bonus a rule adds above the normal rate.
            var bonus = Math.Max(earned - baseline, 0m);
            if (bonus > 0m)
            {
                var contributors = matching.Where(r => r.Mode is StarsRuleMode.Multiplier or StarsRuleMode.FixedPerUnit).ToList();
                var raw = contributors
                    .Select(r => r.Mode == StarsRuleMode.Multiplier ? Math.Max(baseline * (r.Value - 1m), 0m) : Math.Floor(r.Value) * line.Quantity)
                    .ToList();
                var rawSum = raw.Sum();
                var scale = rawSum > 0m ? bonus / rawSum : 0m;
                var paid = 0m;
                for (var i = 0; i < contributors.Count; i++)
                {
                    var charge = Math.Min(raw[i] * scale, Math.Max(Allowance(contributors[i]), 0m));
                    perRule[contributors[i].Id] = perRule.GetValueOrDefault(contributors[i].Id) + charge;
                    paid += charge;
                }

                earned = baseline + paid;
            }

            // The existing per-order cap of a rule limits that rule's lines.
            var capRule = matching.FirstOrDefault(r => r.MaxStarsPerOrder is not null);
            if (capRule is { MaxStarsPerOrder: { } cap })
            {
                var used = perOrderUsed.GetValueOrDefault(capRule.Id);
                earned = Math.Min(earned, Math.Max(cap - used, 0m));
                perOrderUsed[capRule.Id] = used + earned;
            }

            total += earned;
        }

        // Flat bonuses are paid once per order, not per line.
        foreach (var rule in live.Where(r => r.Mode == StarsRuleMode.FlatBonus && FlatScopeMatches(r, lines)))
        {
            var amount = Math.Min(Math.Floor(rule.Value), Math.Max(Allowance(rule), 0m));
            if (rule.MaxStarsPerOrder is { } cap)
            {
                amount = Math.Min(amount, cap);
            }

            if (amount > 0m)
            {
                perRule[rule.Id] = perRule.GetValueOrDefault(rule.Id) + amount;
                total += amount;
            }
        }

        return new StarsEarnResult(ToStars(total), perRule.ToDictionary(p => p.Key, p => (int)Math.Floor(p.Value)));
    }

    private static bool FlatScopeMatches(StarsEarnRule rule, IReadOnlyList<StarsEarnLine> lines) =>
        rule.Scope switch
        {
            StarsRuleScope.Product => lines.Any(l => l.ProductId == rule.ScopeId),
            StarsRuleScope.Category => lines.Any(l => l.CategoryId == rule.ScopeId),
            _ => true,
        };

    private static bool Matches(StarsEarnRule rule, StarsAudience audience, IReadOnlyDictionary<string, int>? rankOrders, StarsOrderContext? ctx)
    {
        if (rule.RequiresElite && !audience.IsElite)
        {
            return false;
        }

        if (rule.MinRankKey is not null
            && !(rankOrders is not null && rankOrders.TryGetValue(rule.MinRankKey, out var needed) && audience.RankOrder >= needed))
        {
            return false;
        }

        if (rule.Condition == StarsRuleCondition.None)
        {
            return true;
        }

        // A rule with a condition can only be judged when the order's facts are known.
        if (ctx is null)
        {
            return false;
        }

        return rule.Condition switch
        {
            StarsRuleCondition.FirstOrder => ctx.PriorDeliveredOrders == 0,
            StarsRuleCondition.SpendThreshold => ctx.EligibleAmountUsd >= rule.ConditionValue,
            StarsRuleCondition.Comeback => ctx.DaysSinceLastOrder is { } days && days >= rule.ConditionValue,
            StarsRuleCondition.NthOrder => rule.ConditionValue >= 1m && (ctx.PriorDeliveredOrders + 1) % (int)rule.ConditionValue == 0,
            StarsRuleCondition.Anniversary => ctx.AnniversaryActive,
            StarsRuleCondition.MinItems => ctx.ItemCount >= rule.ConditionValue,
            StarsRuleCondition.ContainsAllProducts => rule.RequiredProductIds().All(ctx.ProductIds.Contains),
            _ => false,
        };
    }

    private static IEnumerable<StarsEarnRule> Matching(StarsEarnLine line, IEnumerable<StarsEarnRule> liveRules) =>
        liveRules.Where(r => r.Scope switch
        {
            StarsRuleScope.Store => true,
            StarsRuleScope.Product => line.ProductId is { } p && p == r.ScopeId,
            StarsRuleScope.Category => line.CategoryId is { } c && c == r.ScopeId,
            _ => false,
        });

    /// <summary>The winning rule for a line: priority, then scope specificity, then Exclude first.</summary>
    public static StarsEarnRule? Resolve(StarsEarnLine line, IEnumerable<StarsEarnRule> liveRules) =>
        Matching(line, liveRules)
            .OrderByDescending(r => r.Priority)
            .ThenByDescending(r => r.Scope switch { StarsRuleScope.Product => 3, StarsRuleScope.Category => 2, _ => 1 })
            .ThenByDescending(r => r.Mode == StarsRuleMode.Exclude)
            .FirstOrDefault();

    private static int ToStars(decimal value) => (int)Math.Min(Math.Floor(Math.Max(value, 0m)), int.MaxValue);
}
