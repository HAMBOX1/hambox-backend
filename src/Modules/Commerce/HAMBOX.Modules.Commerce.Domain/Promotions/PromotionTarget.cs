using HAMBOX.Domain.Entities;

namespace HAMBOX.Modules.Commerce.Domain.Promotions;

/// <summary>
/// Product or category scope for a promotion.
/// </summary>
public sealed class PromotionTarget : Entity
{
    private PromotionTarget()
    {
    }

    private PromotionTarget(Guid id, Guid promotionId, PromotionTargetType targetType, Guid targetId)
        : base(id)
    {
        PromotionId = promotionId;
        TargetType = targetType;
        TargetId = targetId;
    }

    public Guid PromotionId { get; private set; }
    public PromotionTargetType TargetType { get; private set; }
    public Guid TargetId { get; private set; }

    public static PromotionTarget Create(Guid promotionId, PromotionTargetType targetType, Guid targetId) =>
        new(Guid.NewGuid(), promotionId, targetType, targetId);
}
