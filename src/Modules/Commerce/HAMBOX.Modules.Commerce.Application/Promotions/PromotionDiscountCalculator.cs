using HAMBOX.Modules.Commerce.Domain.Promotions;

namespace HAMBOX.Modules.Commerce.Application.Promotions;

internal static class PromotionDiscountCalculator
{
    public static decimal CalculateDiscount(
        Promotion promotion,
        decimal eligibleSubtotal)
    {
        if (eligibleSubtotal <= 0)
        {
            return 0m;
        }

        var raw = promotion.DiscountType switch
        {
            DiscountType.Percentage => decimal.Round(eligibleSubtotal * (promotion.DiscountValue / 100m), 2),
            DiscountType.FixedAmount => Math.Min(promotion.DiscountValue, eligibleSubtotal),
            _ => 0m,
        };

        var maxDiscount = promotion.GetConditionDecimal(PromotionConditionType.MaxDiscountAmount);
        if (maxDiscount.HasValue)
        {
            raw = Math.Min(raw, maxDiscount.Value);
        }

        return Math.Max(0m, raw);
    }

    public static decimal GetEligibleSubtotal(
        Promotion promotion,
        IReadOnlyList<Models.PromotionCartLine> lines)
    {
        var productTargets = promotion.GetTargetIds(PromotionTargetType.Product);
        var categoryTargets = promotion.GetTargetIds(PromotionTargetType.Category);

        return promotion.Type switch
        {
            PromotionType.Product when productTargets.Count > 0 =>
                lines.Where(l => productTargets.Contains(l.ProductId)).Sum(l => l.LineTotal),
            PromotionType.Category when categoryTargets.Count > 0 =>
                lines.Where(l => l.CategoryId.HasValue && categoryTargets.Contains(l.CategoryId.Value))
                    .Sum(l => l.LineTotal),
            _ => lines.Sum(l => l.LineTotal),
        };
    }
}
