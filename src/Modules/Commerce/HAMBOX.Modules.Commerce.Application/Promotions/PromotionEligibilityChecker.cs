using HAMBOX.Modules.Commerce.Domain.Promotions;

namespace HAMBOX.Modules.Commerce.Application.Promotions;

internal static class PromotionEligibilityChecker
{
    public static string? GetIneligibilityReason(Promotion promotion, PromotionEvaluationRequest request)
    {
        if (!promotion.IsActiveAt(request.UtcNow))
        {
            return "This promotion is not currently active.";
        }

        var minOrder = promotion.GetConditionDecimal(PromotionConditionType.MinOrderAmount);
        if (minOrder.HasValue && request.Subtotal < minOrder.Value)
        {
            return $"Minimum order amount of ${minOrder.Value:F2} is required.";
        }

        if (promotion.HasCondition(PromotionConditionType.FirstPurchaseOnly) && !request.IsFirstPurchase)
        {
            return "This promotion is only valid for first-time purchases.";
        }

        if (promotion.HasCondition(PromotionConditionType.MembershipRequired) && !request.Membership.HasActiveMembership)
        {
            return "An active membership is required for this promotion.";
        }

        var usageLimit = promotion.GetConditionInt(PromotionConditionType.UsageLimit);
        if (usageLimit.HasValue && request.GlobalRedemptionCount >= usageLimit.Value)
        {
            return "This promotion has reached its usage limit.";
        }

        var perUserLimit = promotion.GetConditionInt(PromotionConditionType.PerUserLimit);
        if (perUserLimit.HasValue && request.UserId is not null && request.UserRedemptionCount >= perUserLimit.Value)
        {
            return "You have already used this promotion the maximum number of times.";
        }

        if (request.CountryCode is not null &&
            promotion.Conditions.FirstOrDefault(c => c.ConditionType == PromotionConditionType.CountryCode) is { } countryCondition &&
            !string.Equals(countryCondition.Value, request.CountryCode, StringComparison.OrdinalIgnoreCase))
        {
            return "This promotion is not available in your country.";
        }

        return null;
    }
}
