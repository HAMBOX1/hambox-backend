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
        string payloadJson,
        string? signatureVerificationResult,
        string? gatewayReference,
        string? processingResult)
        : base(id)
    {
        SignatureVerificationResult = signatureVerificationResult;
        GatewayReference = gatewayReference;
        ProcessingResult = processingResult;
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

    /// <summary>How the callback's authenticity was established (Scope §14.3): HMAC valid, verified against the gateway's status API, ...</summary>
    public string? SignatureVerificationResult { get; private set; }

    /// <summary>The gateway's own reference for the payment (their transaction / track / invoice id).</summary>
    public string? GatewayReference { get; private set; }

    /// <summary>What HAMBOX did with it: Paid, Failed, Pending, AmountMismatch, Duplicate, ...</summary>
    public string? ProcessingResult { get; private set; }

    public static OrderPaymentCallback Create(
        Guid orderId,
        string provider,
        string eventType,
        string status,
        string? transactionId,
        string payloadJson,
        string? signatureVerificationResult = null,
        string? gatewayReference = null,
        string? processingResult = null)
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
            payloadJson,
            signatureVerificationResult,
            gatewayReference,
            processingResult);
    }
}
