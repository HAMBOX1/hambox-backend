using HAMBOX.Domain.Entities;

namespace HAMBOX.Modules.Commerce.Domain.Account;

public enum StarsRuleScope
{
    Store = 0,
    Product = 1,
    Category = 2,
}

public enum StarsRuleMode
{
    /// <summary>Multiplies the base earning rate (×2, ×3, …). Value is the multiplier.</summary>
    Multiplier = 0,

    /// <summary>A fixed number of Stars per unit bought, regardless of price. Value is Stars per unit.</summary>
    FixedPerUnit = 1,

    /// <summary>The item earns nothing (the explicit 0-Stars exclusion).</summary>
    Exclude = 2,

    /// <summary>
    /// A flat number of Stars added once to an order that satisfies the rule's scope and condition (Buy-2-get-bonus, group
    /// unlocks, first-order, comeback, anniversary, spend-threshold and Nth-order campaigns). Value is the Stars.
    /// </summary>
    FlatBonus = 3,
}

/// <summary>An extra requirement a rule needs before it applies to an order — what makes it a first-order, comeback, bundle… campaign.</summary>
public enum StarsRuleCondition
{
    None = 0,

    /// <summary>The customer has no earlier delivered order.</summary>
    FirstOrder = 1,

    /// <summary>The order's eligible amount (USD) reaches the condition value.</summary>
    SpendThreshold = 2,

    /// <summary>The customer's previous delivered order was at least this many days ago.</summary>
    Comeback = 3,

    /// <summary>Every Nth delivered order (N = condition value).</summary>
    NthOrder = 4,

    /// <summary>The order falls within three days of the customer's registration anniversary (one year or more after signing up).</summary>
    Anniversary = 5,

    /// <summary>The order holds at least this many items.</summary>
    MinItems = 6,

    /// <summary>The order contains every one of the listed products (a bundle or group unlock).</summary>
    ContainsAllProducts = 7,
}

/// <summary>
/// An admin-managed rule deciding how many Stars an order line earns. Rules are resolved per line by
/// <c>StarsEarnCalculator</c>: highest <see cref="Priority"/> wins, then the more specific scope (Product over
/// Category over Store), then Exclude over the other modes. A Category rule covers products added to the category
/// later, because it is matched on the product's current category at award time.
/// </summary>
public sealed class StarsEarnRule : AggregateRoot
{
    private StarsEarnRule()
    {
    }

    private StarsEarnRule(Guid id)
        : base(id)
    {
    }

    public string Name { get; private set; } = string.Empty;

    public StarsRuleScope Scope { get; private set; }

    /// <summary>The product or category id; null for <see cref="StarsRuleScope.Store"/>.</summary>
    public Guid? ScopeId { get; private set; }

    public StarsRuleMode Mode { get; private set; }

    public decimal Value { get; private set; }

    public int Priority { get; private set; }

    public bool IsActive { get; private set; }

    public DateTimeOffset? StartsOnUtc { get; private set; }

    public DateTimeOffset? EndsOnUtc { get; private set; }

    /// <summary>Only customers at or above this loyalty rank earn from the rule; null = everyone.</summary>
    public string? MinRankKey { get; private set; }

    /// <summary>Only active ELITE members earn from the rule.</summary>
    public bool RequiresElite { get; private set; }

    /// <summary>The most Stars this rule may contribute to a single order; null = no cap.</summary>
    public int? MaxStarsPerOrder { get; private set; }

    public StarsRuleCondition Condition { get; private set; }

    public decimal ConditionValue { get; private set; }

    /// <summary>Comma-separated product ids for <see cref="StarsRuleCondition.ContainsAllProducts"/>.</summary>
    public string? ConditionProductIds { get; private set; }

    /// <summary>The most bonus Stars this rule may pay out across all customers; null = unlimited.</summary>
    public int? BudgetStars { get; private set; }

    /// <summary>The most bonus Stars this rule may pay out to one customer; null = unlimited.</summary>
    public int? MaxStarsPerCustomer { get; private set; }

    /// <summary>When customers who follow the products were told the rule started (so they are told once).</summary>
    public DateTimeOffset? AnnouncedOnUtc { get; private set; }

    public void SetCampaign(StarsRuleCondition condition, decimal conditionValue, IEnumerable<Guid>? productIds, int? budgetStars, int? maxStarsPerCustomer)
    {
        if (budgetStars is < 0 || maxStarsPerCustomer is < 0 || conditionValue < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(budgetStars));
        }

        var ids = productIds?.Distinct().ToList() ?? [];
        if (condition == StarsRuleCondition.ContainsAllProducts && ids.Count == 0)
        {
            throw new ArgumentException("Choose the products the order must contain.", nameof(productIds));
        }

        if (condition is StarsRuleCondition.SpendThreshold or StarsRuleCondition.Comeback or StarsRuleCondition.NthOrder or StarsRuleCondition.MinItems
            && conditionValue <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(conditionValue));
        }

        Condition = condition;
        ConditionValue = conditionValue;
        ConditionProductIds = condition == StarsRuleCondition.ContainsAllProducts ? string.Join(',', ids) : null;
        BudgetStars = budgetStars;
        MaxStarsPerCustomer = maxStarsPerCustomer;
    }

    public IReadOnlyList<Guid> RequiredProductIds() =>
        string.IsNullOrEmpty(ConditionProductIds)
            ? []
            : ConditionProductIds.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(Guid.Parse).ToList();

    public void MarkAnnounced() => AnnouncedOnUtc = DateTimeOffset.UtcNow;

    public void SetAudience(string? minRankKey, bool requiresElite, int? maxStarsPerOrder)
    {
        if (maxStarsPerOrder is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxStarsPerOrder));
        }

        MinRankKey = string.IsNullOrWhiteSpace(minRankKey) ? null : minRankKey.Trim().ToLowerInvariant();
        RequiresElite = requiresElite;
        MaxStarsPerOrder = maxStarsPerOrder;
    }

    public static StarsEarnRule Create(
        string name, StarsRuleScope scope, Guid? scopeId, StarsRuleMode mode, decimal value, int priority,
        bool isActive, DateTimeOffset? startsOnUtc, DateTimeOffset? endsOnUtc)
    {
        var rule = new StarsEarnRule(Guid.NewGuid());
        rule.Apply(name, scope, scopeId, mode, value, priority, isActive, startsOnUtc, endsOnUtc);
        return rule;
    }

    public void Apply(
        string name, StarsRuleScope scope, Guid? scopeId, StarsRuleMode mode, decimal value, int priority,
        bool isActive, DateTimeOffset? startsOnUtc, DateTimeOffset? endsOnUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if ((scope == StarsRuleScope.Store) != (scopeId is null))
        {
            throw new ArgumentException("A Store rule has no scope id; Product and Category rules require one.", nameof(scopeId));
        }

        if (mode == StarsRuleMode.FlatBonus && scope == StarsRuleScope.Store && scopeId is not null)
        {
            throw new ArgumentException("A Store rule has no scope id.", nameof(scopeId));
        }

        if (mode != StarsRuleMode.Exclude && value < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(value));
        }

        if (startsOnUtc is { } s && endsOnUtc is { } e && e <= s)
        {
            throw new ArgumentException("The end must be after the start.", nameof(endsOnUtc));
        }

        Name = name.Trim();
        Scope = scope;
        ScopeId = scopeId;
        Mode = mode;
        Value = mode == StarsRuleMode.Exclude ? 0m : value;
        Priority = priority;
        IsActive = isActive;
        StartsOnUtc = startsOnUtc;
        EndsOnUtc = endsOnUtc;
    }

    public void SetActive(bool isActive) => IsActive = isActive;

    public bool IsLiveAt(DateTimeOffset now) =>
        IsActive && (StartsOnUtc is null || StartsOnUtc <= now) && (EndsOnUtc is null || EndsOnUtc > now);
}
