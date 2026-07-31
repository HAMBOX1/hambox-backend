namespace HAMBOX.Modules.Commerce.Application.Contracts;

/// <summary>
/// Represents a shopping cart line item.
/// </summary>
public sealed record CartItemDto(
    Guid ProductId,
    Guid? ProductVariantId,
    string? VariantSku,
    string ProductNameEn,
    int Quantity,
    decimal UnitPrice,
    decimal LineTotal,
    string? Platform = null,
    string? Region = null,
    string? Edition = null,
    string? VariantSummary = null,
    string? ImageUrl = null);

/// <summary>
/// Represents calculated cart totals.
/// </summary>
public sealed record CartTotalsDto(
    decimal Subtotal,
    decimal TotalDiscount,
    decimal Tax,
    decimal Total,
    int ItemCount,
    IReadOnlyList<AppliedPromotionDto> AppliedPromotions,
    IReadOnlyList<string> ValidationErrors,
    string? AppliedCouponCode);

/// <summary>
/// Represents an applied promotion in cart totals.
/// </summary>
public sealed record AppliedPromotionDto(
    Guid PromotionId,
    string Name,
    string Type,
    string? CouponCode,
    decimal DiscountAmount,
    bool IsAutomatic,
    string? Description);

/// <summary>
/// Represents a shopping cart response.
/// </summary>
public sealed record CartDto(
    Guid? CartId,
    string? GuestSessionId,
    IReadOnlyList<CartItemDto> Items,
    CartTotalsDto Totals);
