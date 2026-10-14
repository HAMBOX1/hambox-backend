using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Domain.Orders;
using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Commerce.Application.Features.Checkout.RecordPaymentWebhook;

/// <summary>
/// Writes one <see cref="PaymentWebhookLog"/> row. Sent by the anonymous webhook endpoints for callbacks that are
/// rejected before any processing (invalid signature, disallowed IP); processed callbacks are logged by their handlers.
/// </summary>
public sealed record RecordPaymentWebhookCommand(
    string Provider,
    string SignatureVerificationResult,
    string ProcessingResult,
    string? GatewayReference,
    string? MerchantReference,
    string? RawPayload,
    string? SourceIp) : IRequest<Result>;

internal sealed class RecordPaymentWebhookCommandHandler(ICommerceDbContext db)
    : IRequestHandler<RecordPaymentWebhookCommand, Result>
{
    public async Task<Result> Handle(RecordPaymentWebhookCommand request, CancellationToken cancellationToken)
    {
        db.PaymentWebhookLogs.Add(PaymentWebhookLog.Create(
            request.Provider,
            request.SignatureVerificationResult,
            request.ProcessingResult,
            request.GatewayReference,
            request.MerchantReference,
            request.RawPayload,
            request.SourceIp));

        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
