using HAMBOX.Modules.Commerce.Application.Points;
using HAMBOX.Modules.Commerce.Domain.Account;

namespace HAMBOX.UnitTests.Commerce.Points;

public sealed class StarsEarnCalculatorTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;
    private static readonly Guid Xbox = Guid.NewGuid();
    private static readonly Guid Trial = Guid.NewGuid();
    private static readonly Guid Other = Guid.NewGuid();

    private static StarsEarnRule Rule(StarsRuleScope scope, Guid? id, StarsRuleMode mode, decimal value, int priority = 0, bool active = true) =>
        StarsEarnRule.Create("r", scope, id, mode, value, priority, active, null, null);

    [Fact]
    public void No_rules_equals_legacy_floor_of_amount_times_rate()
    {
        var lines = new[] { new StarsEarnLine(Guid.NewGuid(), null, 1, 33.33m), new StarsEarnLine(Guid.NewGuid(), null, 1, 10m) };

        Assert.Equal(43, StarsEarnCalculator.Calculate(lines, 43.33m, 1m, [], Now));
    }

    [Fact]
    public void Category_x2_with_excluded_sku_earns_zero_on_it_and_double_on_the_rest()
    {
        var rules = new[]
        {
            Rule(StarsRuleScope.Category, Xbox, StarsRuleMode.Multiplier, 2m),
            Rule(StarsRuleScope.Product, Trial, StarsRuleMode.Exclude, 0m, priority: 1000),
        };
        var lines = new[]
        {
            new StarsEarnLine(Trial, Xbox, 1, 10m),
            new StarsEarnLine(Other, Xbox, 1, 10m),
        };

        Assert.Equal(20, StarsEarnCalculator.Calculate(lines, 20m, 1m, rules, Now));
    }

    [Fact]
    public void Fixed_per_unit_ignores_price_and_scales_with_quantity()
    {
        var rules = new[] { Rule(StarsRuleScope.Product, Trial, StarsRuleMode.FixedPerUnit, 20m) };

        Assert.Equal(60, StarsEarnCalculator.Calculate([new StarsEarnLine(Trial, null, 3, 90m)], 90m, 1m, rules, Now));
    }

    [Fact]
    public void Higher_priority_wins_and_inactive_or_expired_rules_are_ignored()
    {
        var expired = StarsEarnRule.Create("e", StarsRuleScope.Store, null, StarsRuleMode.Multiplier, 10m, 99, true, null, Now.AddDays(-1));
        var rules = new[]
        {
            Rule(StarsRuleScope.Store, null, StarsRuleMode.Multiplier, 2m, priority: 1),
            Rule(StarsRuleScope.Store, null, StarsRuleMode.Multiplier, 3m, priority: 5),
            Rule(StarsRuleScope.Store, null, StarsRuleMode.Multiplier, 50m, priority: 50, active: false),
            expired,
        };

        Assert.Equal(30, StarsEarnCalculator.Calculate([new StarsEarnLine(Other, null, 1, 10m)], 10m, 1m, rules, Now));
    }

    [Fact]
    public void Rule_limited_to_a_rank_or_to_elite_members_only_applies_to_them()
    {
        var gold = StarsEarnRule.Create("g", StarsRuleScope.Store, null, StarsRuleMode.Multiplier, 3m, 0, true, null, null);
        gold.SetAudience("gold", false, null);
        var elite = StarsEarnRule.Create("e", StarsRuleScope.Store, null, StarsRuleMode.Multiplier, 5m, 0, true, null, null);
        elite.SetAudience(null, true, null);
        var orders = new Dictionary<string, int> { ["normal"] = 0, ["gold"] = 3 };
        var lines = new[] { new StarsEarnLine(Other, null, 1, 10m) };

        Assert.Equal(10, StarsEarnCalculator.Calculate(lines, 10m, 1m, [gold], Now, new StarsAudience(1, false), orders));
        Assert.Equal(30, StarsEarnCalculator.Calculate(lines, 10m, 1m, [gold], Now, new StarsAudience(3, false), orders));
        Assert.Equal(10, StarsEarnCalculator.Calculate(lines, 10m, 1m, [elite], Now, new StarsAudience(3, false), orders));
        Assert.Equal(50, StarsEarnCalculator.Calculate(lines, 10m, 1m, [elite], Now, new StarsAudience(0, true), orders));
    }

    [Fact]
    public void Per_order_cap_limits_what_a_single_rule_can_add()
    {
        var capped = StarsEarnRule.Create("c", StarsRuleScope.Store, null, StarsRuleMode.Multiplier, 10m, 0, true, null, null);
        capped.SetAudience(null, false, 40);

        Assert.Equal(40, StarsEarnCalculator.Calculate([new StarsEarnLine(Other, null, 1, 100m)], 100m, 1m, [capped], Now));
    }

    private static StarsOrderContext Ctx(int prior = 0, int? days = null, int items = 1, decimal amount = 10m, bool anniversary = false, params Guid[] products) =>
        new(amount, items, prior, days, anniversary, products.ToHashSet());

    private static StarsEarnRule Campaign(StarsRuleMode mode, decimal value, StarsRuleCondition condition, decimal conditionValue = 0m, params Guid[] products)
    {
        var rule = StarsEarnRule.Create("c", StarsRuleScope.Store, null, mode, value, 0, true, null, null);
        rule.SetCampaign(condition, conditionValue, products, null, null);
        return rule;
    }

    [Fact]
    public void First_order_comeback_nth_and_anniversary_campaigns_apply_only_when_the_condition_holds()
    {
        var lines = new[] { new StarsEarnLine(Other, null, 1, 10m) };
        var first = Campaign(StarsRuleMode.FlatBonus, 25m, StarsRuleCondition.FirstOrder);
        var comeback = Campaign(StarsRuleMode.FlatBonus, 40m, StarsRuleCondition.Comeback, 90m);
        var third = Campaign(StarsRuleMode.FlatBonus, 15m, StarsRuleCondition.NthOrder, 3m);
        var anniversary = Campaign(StarsRuleMode.FlatBonus, 60m, StarsRuleCondition.Anniversary);

        int Run(StarsEarnRule rule, StarsOrderContext ctx) => StarsEarnCalculator.CalculateDetailed(lines, 10m, 1m, [rule], Now, context: ctx).Total;

        Assert.Equal(35, Run(first, Ctx(prior: 0)));
        Assert.Equal(10, Run(first, Ctx(prior: 2)));
        Assert.Equal(50, Run(comeback, Ctx(prior: 4, days: 120)));
        Assert.Equal(10, Run(comeback, Ctx(prior: 4, days: 30)));
        Assert.Equal(25, Run(third, Ctx(prior: 2)));
        Assert.Equal(10, Run(third, Ctx(prior: 3)));
        Assert.Equal(70, Run(anniversary, Ctx(anniversary: true)));
        Assert.Equal(10, Run(anniversary, Ctx()));
        Assert.Equal(10, StarsEarnCalculator.Calculate(lines, 10m, 1m, [first], Now));
    }

    [Fact]
    public void Bundle_bonus_needs_every_listed_product_and_buy_two_needs_two_items()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var bundle = Campaign(StarsRuleMode.FlatBonus, 100m, StarsRuleCondition.ContainsAllProducts, 0m, a, b);
        var buyTwo = Campaign(StarsRuleMode.FlatBonus, 30m, StarsRuleCondition.MinItems, 2m);
        var lines = new[] { new StarsEarnLine(a, null, 1, 5m), new StarsEarnLine(b, null, 1, 5m) };

        Assert.Equal(110, StarsEarnCalculator.CalculateDetailed(lines, 10m, 1m, [bundle], Now, context: Ctx(items: 2, products: [a, b])).Total);
        Assert.Equal(10, StarsEarnCalculator.CalculateDetailed(lines, 10m, 1m, [bundle], Now, context: Ctx(items: 1, products: [a])).Total);
        Assert.Equal(40, StarsEarnCalculator.CalculateDetailed(lines, 10m, 1m, [buyTwo], Now, context: Ctx(items: 2)).Total);
        Assert.Equal(10, StarsEarnCalculator.CalculateDetailed(lines, 10m, 1m, [buyTwo], Now, context: Ctx(items: 1)).Total);
    }

    [Fact]
    public void Highest_offer_applies_one_rule_while_combine_adds_the_bonuses_up_to_the_cap()
    {
        var x2 = Rule(StarsRuleScope.Store, null, StarsRuleMode.Multiplier, 2m, priority: 1);
        var x3 = Rule(StarsRuleScope.Category, Xbox, StarsRuleMode.Multiplier, 3m, priority: 2);
        var lines = new[] { new StarsEarnLine(Other, Xbox, 1, 100m) };

        Assert.Equal(300, StarsEarnCalculator.CalculateDetailed(lines, 100m, 1m, [x2, x3], Now).Total);
        Assert.Equal(400, StarsEarnCalculator.CalculateDetailed(lines, 100m, 1m, [x2, x3], Now, stackingMode: "Combine").Total);
        Assert.Equal(250, StarsEarnCalculator.CalculateDetailed(lines, 100m, 1m, [x2, x3], Now, stackingMode: "Combine", maxMultiplier: 2.5m).Total);

        var exclude = Rule(StarsRuleScope.Product, Other, StarsRuleMode.Exclude, 0m);
        Assert.Equal(0, StarsEarnCalculator.CalculateDetailed(lines, 100m, 1m, [x2, exclude], Now, stackingMode: "Combine").Total);
    }

    [Fact]
    public void Campaign_budget_and_per_customer_limit_cap_only_the_bonus()
    {
        var rule = Rule(StarsRuleScope.Store, null, StarsRuleMode.Multiplier, 3m);
        var lines = new[] { new StarsEarnLine(Other, null, 1, 100m) };

        var campaignLeft = new Dictionary<Guid, StarsRuleLimit> { [rule.Id] = new(50, null) };
        var customerLeft = new Dictionary<Guid, StarsRuleLimit> { [rule.Id] = new(null, 20) };

        var a = StarsEarnCalculator.CalculateDetailed(lines, 100m, 1m, [rule], Now, limits: campaignLeft);
        var b = StarsEarnCalculator.CalculateDetailed(lines, 100m, 1m, [rule], Now, limits: customerLeft);

        Assert.Equal(150, a.Total);
        Assert.Equal(50, a.PerRule[rule.Id]);
        Assert.Equal(120, b.Total);
        Assert.Equal(20, b.PerRule[rule.Id]);
    }

    [Fact]
    public void Store_wide_exclude_earns_nothing()
    {
        var rules = new[] { Rule(StarsRuleScope.Store, null, StarsRuleMode.Exclude, 0m) };

        Assert.Equal(0, StarsEarnCalculator.Calculate([new StarsEarnLine(Other, null, 1, 10m)], 10m, 1m, rules, Now));
    }
}
