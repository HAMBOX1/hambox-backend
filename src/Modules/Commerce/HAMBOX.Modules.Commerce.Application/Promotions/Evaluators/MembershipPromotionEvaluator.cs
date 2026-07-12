using HAMBOX.Modules.Commerce.Domain.Promotions;

namespace HAMBOX.Modules.Commerce.Application.Promotions.Evaluators;

internal sealed class MembershipPromotionEvaluator : PromotionTypeEvaluatorBase
{
    public override PromotionType PromotionType => PromotionType.Membership;

    public override bool CanEvaluate(Promotion promotion, PromotionEvaluationRequest request) =>
        promotion.Type == PromotionType.Membership &&
        request.Membership.HasActiveMembership &&
        request.Membership.DiscountPercentage is null or 0;

    protected override AppliedPromotionCandidate? EvaluateCore(Promotion promotion, PromotionEvaluationRequest request) =>
        CreateCandidate(
            promotion,
            request,
            coupon: null,
            isAutomatic: true,
            description: promotion.Description ?? "Member discount applied automatically.");
}
