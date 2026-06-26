using HAMBOX.Domain.Entities;
using HAMBOX.Modules.Commerce.Domain.Enums;

namespace HAMBOX.Modules.Commerce.Domain.Orders;

/// <summary>
/// Represents a customer order.
/// </summary>
public sealed class Order : AggregateRoot
{
    private readonly List<OrderItem> _items = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="Order"/> class.
    /// </summary>
    /// <remarks>Required by EF Core.</remarks>
    private Order()
    {
    }

    private Order(
        Guid id,
        string userId,
        string orderNumber,
        string email,
        string country,
        string paymentMethod,
        decimal subtotal,
        decimal discountAmount,
        decimal taxAmount,
        decimal totalAmount)
        : base(id)
    {
        UserId = userId;
        OrderNumber = orderNumber;
        Status = OrderStatus.Pending;
        Email = email;
        Country = country;
        PaymentMethod = paymentMethod;
        Subtotal = subtotal;
        DiscountAmount = discountAmount;
        TaxAmount = taxAmount;
        TotalAmount = totalAmount;
    }

    /// <summary>
    /// Gets the user identifier.
    /// </summary>
    public string UserId { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the unique order number.
    /// </summary>
    public string OrderNumber { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the order status.
    /// </summary>
    public OrderStatus Status { get; private set; }

    /// <summary>
    /// Gets the customer email.
    /// </summary>
    public string Email { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the customer country.
    /// </summary>
    public string Country { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the payment method.
    /// </summary>
    public string PaymentMethod { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the order subtotal before discounts and tax.
    /// </summary>
    public decimal Subtotal { get; private set; }

    /// <summary>
    /// Gets the member discount amount applied.
    /// </summary>
    public decimal DiscountAmount { get; private set; }

    /// <summary>
    /// Gets the tax amount applied.
    /// </summary>
    public decimal TaxAmount { get; private set; }

    /// <summary>
    /// Gets the final total amount.
    /// </summary>
    public decimal TotalAmount { get; private set; }

    /// <summary>
    /// Gets the order line items.
    /// </summary>
    public IReadOnlyCollection<OrderItem> Items => _items.AsReadOnly();

    /// <summary>
    /// Creates a new order.
    /// </summary>
    public static Order Create(
        string userId,
        string orderNumber,
        string email,
        string country,
        string paymentMethod,
        decimal subtotal,
        decimal discountAmount,
        decimal taxAmount,
        decimal totalAmount,
        IEnumerable<(Guid ProductId, string ProductNameEn, int Quantity, decimal UnitPrice)> items)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(orderNumber);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(country);
        ArgumentException.ThrowIfNullOrWhiteSpace(paymentMethod);

        var order = new Order(
            Guid.NewGuid(),
            userId,
            orderNumber,
            email,
            country,
            paymentMethod,
            subtotal,
            discountAmount,
            taxAmount,
            totalAmount);

        foreach (var item in items)
        {
            order._items.Add(OrderItem.Create(
                order.Id,
                item.ProductId,
                item.ProductNameEn,
                item.Quantity,
                item.UnitPrice));
        }

        return order;
    }

    /// <summary>
    /// Marks the order as completed.
    /// </summary>
    public void Complete()
    {
        if (Status != OrderStatus.Pending)
        {
            throw new InvalidOperationException("Only pending orders can be completed.");
        }

        Status = OrderStatus.Completed;
    }
}
