using HAMBOX.Modules.Commerce.Domain.Promotions;

namespace HAMBOX.Modules.Commerce.Application.Promotions.Models;

/// <summary>
/// A promotion that was applied during evaluation.
/// </summary>
public sealed record AppliedPromotionDto(
    Guid PromotionId,
    Guid? CouponCodeId,
    string Name,
    PromotionType Type,
    string? CouponCode,
    decimal DiscountAmount,
    bool IsAutomatic,
    string? Description);
