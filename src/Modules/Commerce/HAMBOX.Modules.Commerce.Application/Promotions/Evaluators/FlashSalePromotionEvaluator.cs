using HAMBOX.Modules.Commerce.Domain.Promotions;

namespace HAMBOX.Modules.Commerce.Application.Promotions.Evaluators;

internal sealed class FlashSalePromotionEvaluator : PromotionTypeEvaluatorBase
{
    public override PromotionType PromotionType => PromotionType.FlashSale;

    protected override AppliedPromotionCandidate? EvaluateCore(Promotion promotion, PromotionEvaluationRequest request) =>
        CreateCandidate(
            promotion,
            request,
            coupon: null,
            isAutomatic: true,
            description: promotion.Description ?? "Flash sale discount applied.");
}
