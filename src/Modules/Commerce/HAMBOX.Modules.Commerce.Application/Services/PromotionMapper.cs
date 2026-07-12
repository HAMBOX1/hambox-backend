using HAMBOX.Modules.Commerce.Application.Contracts.Promotions;
using HAMBOX.Modules.Commerce.Domain.Promotions;

namespace HAMBOX.Modules.Commerce.Application.Services;

internal static class PromotionMapper
{
    public static PromotionListItemDto ToListItem(Promotion promotion) =>
        new(
            promotion.Id,
            promotion.Name,
            promotion.Type.ToString(),
            promotion.DiscountType.ToString(),
            promotion.DiscountValue,
            promotion.Status.ToString(),
            promotion.IsPublished,
            promotion.StartDateUtc,
            promotion.EndDateUtc,
            promotion.TotalRedemptions,
            promotion.CouponCodes.Count,
            promotion.CreatedOnUtc);

    public static PromotionDetailDto ToDetail(Promotion promotion) =>
        new(
            promotion.Id,
            promotion.Name,
            promotion.Description,
            promotion.Type.ToString(),
            promotion.DiscountType.ToString(),
            promotion.DiscountValue,
            promotion.Status.ToString(),
            promotion.IsPublished,
            promotion.StartDateUtc,
            promotion.EndDateUtc,
            promotion.TotalRedemptions,
            promotion.Conditions.Select(c => new PromotionConditionDto(c.ConditionType.ToString(), c.Value)).ToList(),
            promotion.Targets.Select(t => new PromotionTargetDto(t.TargetType.ToString(), t.TargetId)).ToList(),
            promotion.CouponCodes.Select(ToCouponDto).ToList(),
            promotion.CreatedOnUtc,
            promotion.ModifiedOnUtc);

    public static CouponCodeDto ToCouponDto(CouponCode coupon) =>
        new(
            coupon.Id,
            coupon.Code,
            coupon.IsSingleUse,
            coupon.MaxUses,
            coupon.PerUserMaxUses,
            coupon.UsedCount,
            coupon.ExpiresOnUtc,
            coupon.IsActive);

    public static PromotionType ParseType(string type) =>
        Enum.TryParse<PromotionType>(type, ignoreCase: true, out var parsed)
            ? parsed
            : throw new ArgumentException($"Invalid promotion type '{type}'.");

    public static DiscountType ParseDiscountType(string type) =>
        Enum.TryParse<DiscountType>(type, ignoreCase: true, out var parsed)
            ? parsed
            : throw new ArgumentException($"Invalid discount type '{type}'.");

    public static PromotionStatus? ParseStatusFilter(string? status) =>
        string.IsNullOrWhiteSpace(status) || status.Equals("all", StringComparison.OrdinalIgnoreCase)
            ? null
            : Enum.TryParse<PromotionStatus>(status, ignoreCase: true, out var parsed)
                ? parsed
                : throw new ArgumentException($"Invalid status filter '{status}'.");

    public static IReadOnlyList<(PromotionConditionType Type, string Value)> ParseConditions(
        IReadOnlyList<PromotionConditionDto>? conditions)
    {
        if (conditions is null || conditions.Count == 0)
        {
            return [];
        }

        return conditions
            .Select(c => (
                Enum.Parse<PromotionConditionType>(c.Type, ignoreCase: true),
                c.Value))
            .ToList();
    }

    public static IReadOnlyList<(PromotionTargetType Type, Guid TargetId)> ParseTargets(
        IReadOnlyList<PromotionTargetDto>? targets)
    {
        if (targets is null || targets.Count == 0)
        {
            return [];
        }

        return targets
            .Select(t => (
                Enum.Parse<PromotionTargetType>(t.Type, ignoreCase: true),
                t.TargetId))
            .ToList();
    }
}
