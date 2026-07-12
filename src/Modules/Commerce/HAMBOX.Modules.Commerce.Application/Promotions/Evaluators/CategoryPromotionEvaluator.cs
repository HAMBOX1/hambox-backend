using HAMBOX.Modules.Commerce.Domain.Promotions;

namespace HAMBOX.Modules.Commerce.Application.Promotions.Evaluators;

internal sealed class CategoryPromotionEvaluator : PromotionTypeEvaluatorBase
{
    public override PromotionType PromotionType => PromotionType.Category;

    public override bool CanEvaluate(Promotion promotion, PromotionEvaluationRequest request) =>
        promotion.Type == PromotionType.Category && promotion.GetTargetIds(PromotionTargetType.Category).Count > 0;

    protected override AppliedPromotionCandidate? EvaluateCore(Promotion promotion, PromotionEvaluationRequest request) =>
        CreateCandidate(promotion, request, coupon: null, isAutomatic: true);
}
