using HAMBOX.Domain.Entities;

namespace HAMBOX.Modules.Commerce.Domain.Promotions;

/// <summary>
/// Snapshot of a promotion applied to an order at checkout.
/// </summary>
public sealed class OrderAppliedPromotion : Entity
{
    private OrderAppliedPromotion()
    {
    }

    private OrderAppliedPromotion(
        Guid id,
        Guid orderId,
        Guid promotionId,
        Guid? couponCodeId,
        string promotionName,
        PromotionType promotionType,
        decimal discountAmount,
        string? couponCode)
        : base(id)
    {
        OrderId = orderId;
        PromotionId = promotionId;
        CouponCodeId = couponCodeId;
        PromotionName = promotionName;
        PromotionType = promotionType;
        DiscountAmount = discountAmount;
        CouponCode = couponCode;
    }

    public Guid OrderId { get; private set; }
    public Guid PromotionId { get; private set; }
    public Guid? CouponCodeId { get; private set; }
    public string PromotionName { get; private set; } = string.Empty;
    public PromotionType PromotionType { get; private set; }
    public decimal DiscountAmount { get; private set; }
    public string? CouponCode { get; private set; }

    public static OrderAppliedPromotion Create(
        Guid orderId,
        Guid promotionId,
        Guid? couponCodeId,
        string promotionName,
        PromotionType promotionType,
        decimal discountAmount,
        string? couponCode) =>
        new(Guid.NewGuid(), orderId, promotionId, couponCodeId, promotionName, promotionType, discountAmount, couponCode);
}
