using HAMBOX.Domain.Entities;

namespace HAMBOX.Modules.Commerce.Domain.Orders;

/// <summary>
/// Represents a line item on an order.
/// </summary>
public sealed class OrderItem : Entity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OrderItem"/> class.
    /// </summary>
    /// <remarks>Required by EF Core.</remarks>
    private OrderItem()
    {
    }

    private OrderItem(
        Guid id,
        Guid orderId,
        Guid productId,
        string productNameEn,
        int quantity,
        decimal unitPrice,
        decimal lineTotal)
        : base(id)
    {
        OrderId = orderId;
        ProductId = productId;
        ProductNameEn = productNameEn;
        Quantity = quantity;
        UnitPrice = unitPrice;
        LineTotal = lineTotal;
    }

    /// <summary>
    /// Gets the order identifier.
    /// </summary>
    public Guid OrderId { get; private set; }

    /// <summary>
    /// Gets the product identifier.
    /// </summary>
    public Guid ProductId { get; private set; }

    /// <summary>
    /// Gets the product name in English at the time of purchase.
    /// </summary>
    public string ProductNameEn { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the ordered quantity.
    /// </summary>
    public int Quantity { get; private set; }

    /// <summary>
    /// Gets the unit price at the time of purchase.
    /// </summary>
    public decimal UnitPrice { get; private set; }

    /// <summary>
    /// Gets the line total.
    /// </summary>
    public decimal LineTotal { get; private set; }

    /// <summary>
    /// Creates a new order item.
    /// </summary>
    internal static OrderItem Create(
        Guid orderId,
        Guid productId,
        string productNameEn,
        int quantity,
        decimal unitPrice)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(productNameEn);

        if (orderId == Guid.Empty)
        {
            throw new ArgumentException("Order identifier must not be empty.", nameof(orderId));
        }

        if (productId == Guid.Empty)
        {
            throw new ArgumentException("Product identifier must not be empty.", nameof(productId));
        }

        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be greater than zero.");
        }

        ArgumentOutOfRangeException.ThrowIfNegative(unitPrice);

        var lineTotal = unitPrice * quantity;
        return new OrderItem(Guid.NewGuid(), orderId, productId, productNameEn, quantity, unitPrice, lineTotal);
    }
}
