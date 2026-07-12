using HAMBOX.Modules.Commerce.Domain.Promotions;

namespace HAMBOX.Modules.Commerce.Application.Promotions.Evaluators;

internal abstract class PromotionTypeEvaluatorBase : IPromotionTypeEvaluator
{
    public abstract PromotionType PromotionType { get; }

    public virtual bool CanEvaluate(Promotion promotion, PromotionEvaluationRequest request) =>
        promotion.Type == PromotionType;

    public AppliedPromotionCandidate? Evaluate(Promotion promotion, PromotionEvaluationRequest request)
    {
        if (!CanEvaluate(promotion, request))
        {
            return null;
        }

        if (PromotionEligibilityChecker.GetIneligibilityReason(promotion, request) is not null)
        {
            return null;
        }

        return EvaluateCore(promotion, request);
    }

    protected AppliedPromotionCandidate? CreateCandidate(
        Promotion promotion,
        PromotionEvaluationRequest request,
        CouponCode? coupon,
        bool isAutomatic,
        string? description = null)
    {
        var eligibleSubtotal = PromotionDiscountCalculator.GetEligibleSubtotal(promotion, request.Lines);
        var discount = PromotionDiscountCalculator.CalculateDiscount(promotion, eligibleSubtotal);
        if (discount <= 0)
        {
            return null;
        }

        return new AppliedPromotionCandidate(
            promotion.Id,
            coupon?.Id,
            promotion.Name,
            promotion.Type,
            coupon?.Code,
            discount,
            isAutomatic,
            description ?? promotion.Description);
    }

    protected abstract AppliedPromotionCandidate? EvaluateCore(Promotion promotion, PromotionEvaluationRequest request);
}
