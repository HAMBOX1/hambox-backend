using HAMBOX.Domain.Entities;

namespace HAMBOX.Modules.Commerce.Domain.Account;

/// <summary>The bonus Stars one rule contributed to one order — what budgets and per-customer limits are counted against.</summary>
public sealed class StarsRuleAward : Entity
{
    private StarsRuleAward()
    {
    }

    private StarsRuleAward(Guid id)
        : base(id)
    {
    }

    public Guid RuleId { get; private set; }

    public string UserId { get; private set; } = string.Empty;

    public Guid OrderId { get; private set; }

    public int Stars { get; private set; }

    public static StarsRuleAward Create(Guid ruleId, string userId, Guid orderId, int stars) =>
        new(Guid.NewGuid()) { RuleId = ruleId, UserId = userId, OrderId = orderId, Stars = stars };
}
