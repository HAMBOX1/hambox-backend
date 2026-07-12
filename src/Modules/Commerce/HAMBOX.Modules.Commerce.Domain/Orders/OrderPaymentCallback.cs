using HAMBOX.Domain.Entities;

namespace HAMBOX.Modules.Commerce.Domain.Orders;

/// <summary>
/// Stores payment provider callback / capture payloads for admin reconciliation.
/// </summary>
public sealed class OrderPaymentCallback : Entity
{
    private OrderPaymentCallback()
    {
    }

    private OrderPaymentCallback(
        Guid id,
        Guid orderId,
        string provider,
        string eventType,
        string status,
        string? transactionId,
        string payloadJson)
        : base(id)
    {
        OrderId = orderId;
        Provider = provider;
        EventType = eventType;
        Status = status;
        TransactionId = transactionId;
        PayloadJson = payloadJson;
        OccurredOnUtc = DateTimeOffset.UtcNow;
    }

    public Guid OrderId { get; private set; }

    public string Provider { get; private set; } = string.Empty;

    public string EventType { get; private set; } = string.Empty;

    public string Status { get; private set; } = string.Empty;

    public string? TransactionId { get; private set; }

    public string PayloadJson { get; private set; } = string.Empty;

    public DateTimeOffset OccurredOnUtc { get; private set; }

    public static OrderPaymentCallback Create(
        Guid orderId,
        string provider,
        string eventType,
        string status,
        string? transactionId,
        string payloadJson)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);
        ArgumentException.ThrowIfNullOrWhiteSpace(status);
        ArgumentException.ThrowIfNullOrWhiteSpace(payloadJson);

        return new OrderPaymentCallback(
            Guid.NewGuid(),
            orderId,
            provider,
            eventType,
            status,
            transactionId,
            payloadJson);
    }
}
