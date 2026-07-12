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
        Kind = OrderKind.Product;
        Status = OrderStatus.Pending;
        Email = email;
        Country = country;
        PaymentMethod = paymentMethod;
        PaymentStatus = Enums.PaymentStatus.Pending;
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
    /// Gets the payment provider name (e.g. Development, Stripe).
    /// </summary>
    public string? PaymentProvider { get; private set; }

    /// <summary>
    /// Gets the external payment transaction identifier.
    /// </summary>
    public string? PaymentTransactionId { get; private set; }

    /// <summary>
    /// Gets the payment status.
    /// </summary>
    public PaymentStatus PaymentStatus { get; private set; }

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

    public OrderKind Kind { get; private set; }

    public MembershipCheckoutAction? MembershipAction { get; private set; }

    public Guid? MembershipPlanId { get; private set; }

    public Guid? MembershipSubscriptionId { get; private set; }

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
        IEnumerable<(Guid ProductId, string ProductNameEn, int Quantity, decimal UnitPrice, Guid? ProductVariantId, string? VariantSku)> items)
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
                item.UnitPrice,
                item.ProductVariantId,
                item.VariantSku));
        }

        return order;
    }

    public static Order CreateForMembership(
        string userId,
        string orderNumber,
        string email,
        string country,
        string paymentMethod,
        decimal totalAmount,
        Guid membershipPlanId,
        string planName,
        MembershipCheckoutAction action,
        Guid? membershipSubscriptionId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(orderNumber);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(country);
        ArgumentException.ThrowIfNullOrWhiteSpace(paymentMethod);
        ArgumentException.ThrowIfNullOrWhiteSpace(planName);

        var order = new Order(
            Guid.NewGuid(),
            userId,
            orderNumber,
            email,
            country,
            paymentMethod,
            totalAmount,
            0m,
            0m,
            totalAmount)
        {
            Kind = OrderKind.Membership,
            MembershipAction = action,
            MembershipPlanId = membershipPlanId,
            MembershipSubscriptionId = membershipSubscriptionId,
        };

        order._items.Add(OrderItem.CreateMembership(order.Id, membershipPlanId, planName, totalAmount));
        return order;
    }

    public void LinkMembershipSubscription(Guid subscriptionId) => MembershipSubscriptionId = subscriptionId;

    /// <summary>
    /// Records a successful payment before order completion.
    /// </summary>
    public void RecordPayment(string provider, string transactionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(transactionId);

        PaymentProvider = provider;
        PaymentTransactionId = transactionId;
        PaymentStatus = Enums.PaymentStatus.Paid;
    }

    /// <summary>
    /// Marks the order as completed.
    /// </summary>
    public void Complete()
    {
        if (Status != OrderStatus.Pending && Status != OrderStatus.Processing)
        {
            throw new InvalidOperationException("Only pending or processing orders can be completed.");
        }

        if (PaymentStatus != Enums.PaymentStatus.Paid)
        {
            throw new InvalidOperationException("Order cannot be completed until payment is captured.");
        }

        Status = OrderStatus.Completed;
    }

    /// <summary>
    /// Marks a paid order as processing.
    /// </summary>
    public void MarkProcessing()
    {
        if (Status == OrderStatus.Cancelled || Status == OrderStatus.Refunded || Status == OrderStatus.Failed)
        {
            throw new InvalidOperationException("Cancelled, refunded, or failed orders cannot be marked as processing.");
        }

        if (PaymentStatus != Enums.PaymentStatus.Paid)
        {
            throw new InvalidOperationException("Only paid orders can be marked as processing.");
        }

        Status = OrderStatus.Processing;
    }

    /// <summary>
    /// Cancels the order when it has not been completed.
    /// </summary>
    public void Cancel()
    {
        if (Status == OrderStatus.Completed || Status == OrderStatus.Refunded)
        {
            throw new InvalidOperationException("Completed or refunded orders cannot be cancelled.");
        }

        Status = OrderStatus.Cancelled;
    }

    /// <summary>
    /// Marks the order as refunded after a successful payment.
    /// </summary>
    public void Refund()
    {
        if (PaymentStatus != Enums.PaymentStatus.Paid && PaymentStatus != Enums.PaymentStatus.Refunded)
        {
            throw new InvalidOperationException("Only paid orders can be refunded.");
        }

        PaymentStatus = Enums.PaymentStatus.Refunded;
        Status = OrderStatus.Refunded;
    }

    /// <summary>
    /// Marks the order as failed.
    /// </summary>
    public void MarkFailed()
    {
        if (Status == OrderStatus.Completed || Status == OrderStatus.Refunded)
        {
            throw new InvalidOperationException("Completed or refunded orders cannot be marked as failed.");
        }

        PaymentStatus = Enums.PaymentStatus.Failed;
        Status = OrderStatus.Failed;
    }

    /// <summary>
    /// Forces order status for admin operations.
    /// </summary>
    public void SetAdminStatus(OrderStatus status)
    {
        switch (status)
        {
            case OrderStatus.Pending:
                Status = OrderStatus.Pending;
                break;
            case OrderStatus.Processing:
                MarkProcessing();
                break;
            case OrderStatus.Completed:
                Complete();
                break;
            case OrderStatus.Cancelled:
                Cancel();
                break;
            case OrderStatus.Refunded:
                Refund();
                break;
            case OrderStatus.Failed:
                MarkFailed();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(status), status, "Unsupported order status.");
        }
    }
}
