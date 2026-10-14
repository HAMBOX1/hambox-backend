using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Domain.Orders;

namespace HAMBOX.Modules.Commerce.Application.Services;

/// <summary>Adds a <see cref="PaymentWebhookLog"/> row for a callback a handler has just processed (caller saves).</summary>
internal static class PaymentWebhookLogRecorder
{
    public static void Add(
        ICommerceDbContext db,
        string provider,
        string signatureVerificationResult,
        string processingResult,
        string? gatewayReference,
        string? merchantReference,
        string? rawPayload,
        Guid? orderId = null,
        Guid? paymentAttemptId = null) =>
        db.PaymentWebhookLogs.Add(PaymentWebhookLog.Create(
            provider, signatureVerificationResult, processingResult, gatewayReference, merchantReference,
            rawPayload, sourceIp: null, orderId, paymentAttemptId));
}
