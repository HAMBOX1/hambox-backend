namespace HAMBOX.Modules.Commerce.Application.Contracts.Promotions;

public sealed record PromotionConditionDto(string Type, string Value);

public sealed record PromotionTargetDto(string Type, Guid TargetId);

public sealed record CouponCodeDto(
    Guid Id,
    string Code,
    bool IsSingleUse,
    int? MaxUses,
    int? PerUserMaxUses,
    int UsedCount,
    DateTime? ExpiresOnUtc,
    bool IsActive);

public sealed record PromotionListItemDto(
    Guid Id,
    string Name,
    string Type,
    string DiscountType,
    decimal DiscountValue,
    string Status,
    bool IsPublished,
    DateTime? StartDateUtc,
    DateTime? EndDateUtc,
    int TotalRedemptions,
    int CouponCount,
    DateTimeOffset CreatedOnUtc);

public sealed record PromotionDetailDto(
    Guid Id,
    string Name,
    string? Description,
    string Type,
    string DiscountType,
    decimal DiscountValue,
    string Status,
    bool IsPublished,
    DateTime? StartDateUtc,
    DateTime? EndDateUtc,
    int TotalRedemptions,
    IReadOnlyList<PromotionConditionDto> Conditions,
    IReadOnlyList<PromotionTargetDto> Targets,
    IReadOnlyList<CouponCodeDto> CouponCodes,
    DateTimeOffset CreatedOnUtc,
    DateTimeOffset? ModifiedOnUtc);

public sealed record PromotionStatisticsDto(
    Guid PromotionId,
    int TotalRedemptions,
    decimal TotalDiscountGiven,
    int UniqueUsers,
    int ActiveCoupons,
    int ExpiredCoupons);

public sealed record PromotionRedemptionDto(
    Guid Id,
    string PromotionName,
    string? CouponCode,
    string? UserId,
    Guid? OrderId,
    decimal DiscountAmount,
    DateTime RedeemedOnUtc);

public sealed record CreatePromotionRequest(
    string Name,
    string? Description,
    string Type,
    string DiscountType,
    decimal DiscountValue,
    DateTime? StartDateUtc,
    DateTime? EndDateUtc,
    IReadOnlyList<PromotionConditionDto>? Conditions,
    IReadOnlyList<PromotionTargetDto>? Targets,
    string? InitialCouponCode);

public sealed record UpdatePromotionRequest(
    string Name,
    string? Description,
    string DiscountType,
    decimal DiscountValue,
    DateTime? StartDateUtc,
    DateTime? EndDateUtc,
    IReadOnlyList<PromotionConditionDto>? Conditions,
    IReadOnlyList<PromotionTargetDto>? Targets);

public sealed record GenerateCouponsRequest(
    int Count,
    string? Prefix,
    bool IsSingleUse,
    int? MaxUses,
    int? PerUserMaxUses,
    DateTime? ExpiresOnUtc);

public sealed record CreateCouponRequest(
    string Code,
    bool IsSingleUse,
    int? MaxUses,
    int? PerUserMaxUses,
    DateTime? ExpiresOnUtc);

public sealed record ValidateCouponRequest(string CouponCode, string? CountryCode);

public sealed record ValidateCouponResponse(
    bool IsValid,
    string? ErrorMessage,
    Guid? PromotionId,
    Guid? CouponCodeId);
