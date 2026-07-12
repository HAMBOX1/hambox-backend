using HAMBOX.Domain.Entities;

namespace HAMBOX.Modules.Commerce.Domain.Promotions;

public enum PromotionAuditAction
{
    Created = 0,
    Updated = 1,
    Deleted = 2,
    Published = 3,
    CouponGenerated = 4,
    CouponRedeemed = 5,
}

/// <summary>
/// Audit trail for promotion and coupon lifecycle events.
/// </summary>
public sealed class PromotionAuditLog : Entity
{
    private PromotionAuditLog()
    {
    }

    private PromotionAuditLog(
        Guid id,
        Guid? promotionId,
        Guid? couponCodeId,
        PromotionAuditAction action,
        string? performedByUserId,
        string? details)
        : base(id)
    {
        PromotionId = promotionId;
        CouponCodeId = couponCodeId;
        Action = action;
        PerformedByUserId = performedByUserId;
        Details = details;
        OccurredOnUtc = DateTime.UtcNow;
    }

    public Guid? PromotionId { get; private set; }
    public Guid? CouponCodeId { get; private set; }
    public PromotionAuditAction Action { get; private set; }
    public string? PerformedByUserId { get; private set; }
    public string? Details { get; private set; }
    public DateTime OccurredOnUtc { get; private set; }

    public static PromotionAuditLog Create(
        Guid? promotionId,
        Guid? couponCodeId,
        PromotionAuditAction action,
        string? performedByUserId,
        string? details = null) =>
        new(Guid.NewGuid(), promotionId, couponCodeId, action, performedByUserId, details);
}
