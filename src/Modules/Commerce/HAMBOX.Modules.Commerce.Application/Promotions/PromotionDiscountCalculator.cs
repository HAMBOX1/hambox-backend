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

        // Targets narrow the discount base for every promotion kind, not only the Product/Category types: a coupon
        // code (or automatic / flash-sale promotion) that names products or categories discounts just those lines —
        // that is what makes a "per-product promo code" mean something.
        if (productTargets.Count > 0)
        {
            var targeted = lines.Where(l => productTargets.Contains(l.ProductId)).Sum(l => l.LineTotal);
            if (categoryTargets.Count == 0)
            {
                return targeted;
            }

            return lines
                .Where(l => productTargets.Contains(l.ProductId)
                    || (l.CategoryId.HasValue && categoryTargets.Contains(l.CategoryId.Value)))
                .Sum(l => l.LineTotal);
        }

        if (categoryTargets.Count > 0)
        {
            return lines
                .Where(l => l.CategoryId.HasValue && categoryTargets.Contains(l.CategoryId.Value))
                .Sum(l => l.LineTotal);
        }

        return lines.Sum(l => l.LineTotal);
    }
}
