using HAMBOX.Modules.Commerce.Application.Memberships.Models;
using HAMBOX.Modules.Commerce.Domain.Promotions;

namespace HAMBOX.Modules.Commerce.Application.Promotions;

/// <summary>
/// Strategy for evaluating a specific promotion type.
/// </summary>
public interface IPromotionTypeEvaluator
{
    PromotionType PromotionType { get; }

    bool CanEvaluate(Promotion promotion, PromotionEvaluationRequest request);

    AppliedPromotionCandidate? Evaluate(Promotion promotion, PromotionEvaluationRequest request);
}

/// <summary>
/// Internal request passed to promotion type evaluators.
/// </summary>
public sealed record PromotionEvaluationRequest(
    IReadOnlyList<Promotions.Models.PromotionCartLine> Lines,
    decimal Subtotal,
    string? UserId,
    string? CountryCode,
    bool IsAuthenticated,
    bool IsFirstPurchase,
    bool HasReferralProfile,
    string? AppliedCouponCode,
    CouponCode? MatchedCoupon,
    int UserRedemptionCount,
    int GlobalRedemptionCount,
    MembershipSnapshot Membership,
    DateTime UtcNow);

/// <summary>
/// A promotion candidate before stacking rules are applied.
/// </summary>
public sealed record AppliedPromotionCandidate(
    Guid PromotionId,
    Guid? CouponCodeId,
    string Name,
    PromotionType Type,
    string? CouponCode,
    decimal DiscountAmount,
    bool IsAutomatic,
    string? Description);
