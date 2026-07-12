using HAMBOX.Modules.Commerce.Domain.Promotions;

namespace HAMBOX.Modules.Commerce.Application.Promotions.Evaluators;

internal sealed class CouponPromotionEvaluator : PromotionTypeEvaluatorBase
{
    public override PromotionType PromotionType => PromotionType.CouponCode;

    public override bool CanEvaluate(Promotion promotion, PromotionEvaluationRequest request) =>
        promotion.Type == PromotionType.CouponCode &&
        request.MatchedCoupon is not null &&
        request.MatchedCoupon.PromotionId == promotion.Id;

    protected override AppliedPromotionCandidate? EvaluateCore(Promotion promotion, PromotionEvaluationRequest request) =>
        CreateCandidate(promotion, request, request.MatchedCoupon, isAutomatic: false);
}
