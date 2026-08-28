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
        decimal lineTotal,
        Guid? selectedSupplierId = null,
        Guid? selectedSupplierProductMappingId = null,
        decimal? supplierBuyingPriceAtOrderTime = null,
        decimal? marginPercentAppliedAtOrderTime = null)
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
        SelectedSupplierId = selectedSupplierId;
        SelectedSupplierProductMappingId = selectedSupplierProductMappingId;
        SupplierBuyingPriceAtOrderTime = supplierBuyingPriceAtOrderTime;
        MarginPercentAppliedAtOrderTime = marginPercentAppliedAtOrderTime;
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
    /// Gets the supplier that was selected to fulfill this line at order-creation time — a frozen
    /// snapshot, never re-read from <c>SupplierProductMapping</c>/<c>SupplierDerivedPrice</c> afterward,
    /// so a later change to supplier cost/margin can never retroactively alter an existing order. Null
    /// for lines that were not priced from a supplier (manual-only variants, membership lines).
    /// </summary>
    public Guid? SelectedSupplierId { get; private set; }

    public Guid? SelectedSupplierProductMappingId { get; private set; }

    /// <summary>The supplier's <c>BuyingPrice</c> (in the platform base currency) at the moment this order was created — see <see cref="SelectedSupplierId"/>.</summary>
    public decimal? SupplierBuyingPriceAtOrderTime { get; private set; }

    /// <summary>The margin percent actually applied to compute <see cref="UnitPrice"/> at order-creation time — see <see cref="SelectedSupplierId"/>.</summary>
    public decimal? MarginPercentAppliedAtOrderTime { get; private set; }

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
        string? variantSku = null,
        Guid? selectedSupplierId = null,
        Guid? selectedSupplierProductMappingId = null,
        decimal? supplierBuyingPriceAtOrderTime = null,
        decimal? marginPercentAppliedAtOrderTime = null)
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
            lineTotal,
            selectedSupplierId,
            selectedSupplierProductMappingId,
            supplierBuyingPriceAtOrderTime,
            marginPercentAppliedAtOrderTime);
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
