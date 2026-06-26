namespace HAMBOX.Modules.Commerce.Application.Contracts;

/// <summary>
/// Represents an order line item.
/// </summary>
public sealed record OrderItemDto(
    Guid ProductId,
    string ProductNameEn,
    int Quantity,
    decimal UnitPrice,
    decimal LineTotal);

/// <summary>
/// Represents an order response.
/// </summary>
public sealed record OrderDto(
    Guid Id,
    string OrderNumber,
    string Status,
    string Email,
    string Country,
    string PaymentMethod,
    decimal Subtotal,
    decimal DiscountAmount,
    decimal TaxAmount,
    decimal TotalAmount,
    IReadOnlyList<OrderItemDto> Items,
    DateTimeOffset CreatedOnUtc);
