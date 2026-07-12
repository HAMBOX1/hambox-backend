using HAMBOX.Domain.Entities;

namespace HAMBOX.Modules.Commerce.Domain.Promotions;

/// <summary>
/// A rule or eligibility condition attached to a promotion.
/// </summary>
public sealed class PromotionCondition : Entity
{
    private PromotionCondition()
    {
    }

    private PromotionCondition(Guid id, Guid promotionId, PromotionConditionType conditionType, string value)
        : base(id)
    {
        PromotionId = promotionId;
        ConditionType = conditionType;
        Value = value;
    }

    public Guid PromotionId { get; private set; }
    public PromotionConditionType ConditionType { get; private set; }
    public string Value { get; private set; } = string.Empty;

    public static PromotionCondition Create(Guid promotionId, PromotionConditionType conditionType, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return new PromotionCondition(Guid.NewGuid(), promotionId, conditionType, value.Trim());
    }
}
