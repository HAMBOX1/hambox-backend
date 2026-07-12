using HAMBOX.Modules.Commerce.Domain.Promotions;

namespace HAMBOX.Modules.Commerce.Application.Promotions.Evaluators;

internal sealed class ReferralRewardPromotionEvaluator : PromotionTypeEvaluatorBase
{
    public override PromotionType PromotionType => PromotionType.ReferralReward;

    public override bool CanEvaluate(Promotion promotion, PromotionEvaluationRequest request) =>
        promotion.Type == PromotionType.ReferralReward && request.HasReferralProfile;

    protected override AppliedPromotionCandidate? EvaluateCore(Promotion promotion, PromotionEvaluationRequest request) =>
        CreateCandidate(
            promotion,
            request,
            coupon: null,
            isAutomatic: true,
            description: promotion.Description ?? "Referral reward discount applied.");
}
