namespace HAMBOX.Modules.Commerce.Application.Contracts;

/// <summary>
/// Represents a shopping cart line item.
/// </summary>
public sealed record CartItemDto(
    Guid ProductId,
    string ProductNameEn,
    int Quantity,
    decimal UnitPrice,
    decimal LineTotal);

/// <summary>
/// Represents calculated cart totals.
/// </summary>
public sealed record CartTotalsDto(
    decimal Subtotal,
    decimal MemberDiscount,
    decimal Tax,
    decimal Total,
    int ItemCount);

/// <summary>
/// Represents a shopping cart response.
/// </summary>
public sealed record CartDto(
    Guid? CartId,
    string? GuestSessionId,
    IReadOnlyList<CartItemDto> Items,
    CartTotalsDto Totals);
