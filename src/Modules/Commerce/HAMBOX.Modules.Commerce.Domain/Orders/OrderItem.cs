using HAMBOX.Domain.Entities;
using HAMBOX.Modules.Commerce.Domain.Enums;

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
        OrderLineItemType lineItemType,
        Guid? productId,
        Guid? membershipPlanId,
        Guid? productVariantId,
        string productNameEn,
        string? variantSku,
        int quantity,
        decimal unitPrice,
        decimal lineTotal)
        : base(id)
    {
        OrderId = orderId;
        LineItemType = lineItemType;
        ProductId = productId;
        MembershipPlanId = membershipPlanId;
        ProductVariantId = productVariantId;
        ProductNameEn = productNameEn;
        VariantSku = variantSku;
        Quantity = quantity;
        UnitPrice = unitPrice;
        LineTotal = lineTotal;
    }

    /// <summary>
    /// Gets the order identifier.
    /// </summary>
    public Guid OrderId { get; private set; }

    public OrderLineItemType LineItemType { get; private set; }

    /// <summary>
    /// Gets the product identifier when this is a product line.
    /// </summary>
    public Guid? ProductId { get; private set; }

    public Guid? MembershipPlanId { get; private set; }

    public Guid? ProductVariantId { get; private set; }

    public string? VariantSku { get; private set; }

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
    /// Creates a new product order item.
    /// </summary>
    internal static OrderItem Create(
        Guid orderId,
        Guid productId,
        string productNameEn,
        int quantity,
        decimal unitPrice,
        Guid? productVariantId = null,
        string? variantSku = null)
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
        return new OrderItem(
            Guid.NewGuid(),
            orderId,
            OrderLineItemType.Product,
            productId,
            null,
            productVariantId,
            productNameEn,
            variantSku,
            quantity,
            unitPrice,
            lineTotal);
    }

    internal static OrderItem CreateMembership(
        Guid orderId,
        Guid membershipPlanId,
        string planName,
        decimal unitPrice)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(planName);

        if (orderId == Guid.Empty)
        {
            throw new ArgumentException("Order identifier must not be empty.", nameof(orderId));
        }

        if (membershipPlanId == Guid.Empty)
        {
            throw new ArgumentException("Membership plan identifier must not be empty.", nameof(membershipPlanId));
        }

        ArgumentOutOfRangeException.ThrowIfNegative(unitPrice);

        return new OrderItem(
            Guid.NewGuid(),
            orderId,
            OrderLineItemType.Membership,
            null,
            membershipPlanId,
            null,
            planName,
            null,
            1,
            unitPrice,
            unitPrice);
    }
}
