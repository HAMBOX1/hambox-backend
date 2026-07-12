using HAMBOX.Modules.Commerce.Domain.Promotions;

namespace HAMBOX.Modules.Commerce.Application.Promotions.Evaluators;

internal sealed class AutomaticPromotionEvaluator : PromotionTypeEvaluatorBase
{
    public override PromotionType PromotionType => PromotionType.Automatic;

    protected override AppliedPromotionCandidate? EvaluateCore(Promotion promotion, PromotionEvaluationRequest request) =>
        CreateCandidate(promotion, request, coupon: null, isAutomatic: true);
}
