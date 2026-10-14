using HAMBOX.Domain.Entities;

namespace HAMBOX.Modules.Commerce.Domain.Orders;

/// <summary>
/// One row for EVERY inbound gateway callback/webhook — including the ones that were rejected (bad signature,
/// disallowed IP) or that matched no order — so a disputed or suspicious callback can always be traced (Scope §14.3).
/// </summary>
public sealed class PaymentWebhookLog : Entity
{
    public const int MaxRawPayloadLength = 8000;

    private PaymentWebhookLog()
    {
    }

    private PaymentWebhookLog(
        Guid id,
        string provider,
        string signatureVerificationResult,
        string processingResult,
        string? gatewayReference,
        string? merchantReference,
        string rawPayload,
        string? sourceIp,
        Guid? orderId,
        Guid? paymentAttemptId)
        : base(id)
    {
        Provider = provider;
        SignatureVerificationResult = signatureVerificationResult;
        ProcessingResult = processingResult;
        GatewayReference = gatewayReference;
        MerchantReference = merchantReference;
        RawPayload = rawPayload;
        SourceIp = sourceIp;
        OrderId = orderId;
        PaymentAttemptId = paymentAttemptId;
        ReceivedOnUtc = DateTimeOffset.UtcNow;
    }

    public string Provider { get; private set; } = string.Empty;

    /// <summary>Valid, Invalid, Missing, or NotApplicable (the gateway has no signature — the status API is the check).</summary>
    public string SignatureVerificationResult { get; private set; } = string.Empty;

    /// <summary>Processed outcome: Paid, Failed, Pending, Duplicate, OrderNotFound, RejectedSignature, RejectedIp, ...</summary>
    public string ProcessingResult { get; private set; } = string.Empty;

    public string? GatewayReference { get; private set; }

    /// <summary>The reference HAMBOX gave the gateway (our order / partner transaction id).</summary>
    public string? MerchantReference { get; private set; }

    public string RawPayload { get; private set; } = string.Empty;

    public string? SourceIp { get; private set; }

    public Guid? OrderId { get; private set; }

    public Guid? PaymentAttemptId { get; private set; }

    public DateTimeOffset ReceivedOnUtc { get; private set; }

    public static PaymentWebhookLog Create(
        string provider,
        string signatureVerificationResult,
        string processingResult,
        string? gatewayReference,
        string? merchantReference,
        string? rawPayload,
        string? sourceIp,
        Guid? orderId = null,
        Guid? paymentAttemptId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(signatureVerificationResult);
        ArgumentException.ThrowIfNullOrWhiteSpace(processingResult);

        var payload = rawPayload ?? string.Empty;
        if (payload.Length > MaxRawPayloadLength)
        {
            payload = payload[..MaxRawPayloadLength];
        }

        return new PaymentWebhookLog(
            Guid.NewGuid(), provider, signatureVerificationResult, processingResult,
            gatewayReference, merchantReference, payload, sourceIp, orderId, paymentAttemptId);
    }
}
