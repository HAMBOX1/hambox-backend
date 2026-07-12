using HAMBOX.Domain.Entities;

namespace HAMBOX.Modules.Commerce.Domain.Promotions;

/// <summary>
/// Records a promotion redemption against an order or cart preview.
/// </summary>
public sealed class PromotionRedemption : Entity
{
    private PromotionRedemption()
    {
    }

    private PromotionRedemption(
        Guid id,
        Guid promotionId,
        Guid? couponCodeId,
        string? userId,
        Guid? orderId,
        decimal discountAmount,
        string promotionName)
        : base(id)
    {
        PromotionId = promotionId;
        CouponCodeId = couponCodeId;
        UserId = userId;
        OrderId = orderId;
        DiscountAmount = discountAmount;
        PromotionName = promotionName;
        RedeemedOnUtc = DateTime.UtcNow;
    }

    public Guid PromotionId { get; private set; }
    public Guid? CouponCodeId { get; private set; }
    public string? UserId { get; private set; }
    public Guid? OrderId { get; private set; }
    public decimal DiscountAmount { get; private set; }
    public string PromotionName { get; private set; } = string.Empty;
    public DateTime RedeemedOnUtc { get; private set; }

    public static PromotionRedemption Create(
        Guid promotionId,
        Guid? couponCodeId,
        string? userId,
        Guid? orderId,
        decimal discountAmount,
        string promotionName) =>
        new(Guid.NewGuid(), promotionId, couponCodeId, userId, orderId, discountAmount, promotionName);
}
