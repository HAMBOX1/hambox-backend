using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Services;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HAMBOX.Modules.Commerce.Application.Features.Checkout.OxaPay;

internal sealed class HandleOxaPayWebhookCommandHandler(
    ICommerceDbContext commerceDbContext,
    OxaPayPaymentVerificationService verificationService,
    ILogger<HandleOxaPayWebhookCommandHandler> logger)
    : IRequestHandler<HandleOxaPayWebhookCommand, Result>
{
    public async Task<Result> Handle(HandleOxaPayWebhookCommand request, CancellationToken cancellationToken)
    {
        var attempt = await commerceDbContext.PaymentAttempts
            .FirstOrDefaultAsync(p => p.Provider == "OxaPay" && p.PartnerTxId == request.OrderId, cancellationToken);

        if (attempt is null)
        {
            // Nothing HAMBOX can reconcile this against. Ack anyway — OxaPay retrying an
            // unresolvable order_id helps no one; log it so it's visible for investigation.
            logger.LogWarning("OxaPay webhook for unknown order_id.");
            PaymentWebhookLogRecorder.Add(
                commerceDbContext, "OxaPay", "Valid", "OrderNotFound",
                request.TrackId, request.OrderId, System.Text.Json.JsonSerializer.Serialize(new { request.OrderId, request.TrackId, request.Status }));
            await commerceDbContext.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }

        var wasResolved = attempt.Status is HAMBOX.Modules.Commerce.Domain.Enums.PaymentAttemptStatus.Succeeded
            or HAMBOX.Modules.Commerce.Domain.Enums.PaymentAttemptStatus.Failed
            or HAMBOX.Modules.Commerce.Domain.Enums.PaymentAttemptStatus.Expired;

        attempt.RecordProviderContext(request.TrackId, null, request.Status, null);
        await commerceDbContext.SaveChangesAsync(cancellationToken);

        // The webhook body's status is never trusted by itself — this re-verifies with OxaPay
        // server-to-server. Safe to call repeatedly (idempotent claim-guarded).
        var alreadyResolved = wasResolved;
        var verification = await verificationService.VerifyAndFinalizeAsync(attempt.Id, cancellationToken);

        var processingResult = alreadyResolved ? "Duplicate" : verification.Outcome switch
        {
            OxaPayVerificationOutcome.Succeeded => "Paid",
            OxaPayVerificationOutcome.Failed => "Failed",
            OxaPayVerificationOutcome.StillPending => "Pending",
            _ => "OrderNotFound",
        };
        PaymentWebhookLogRecorder.Add(
            commerceDbContext, "OxaPay", "Valid", processingResult,
            request.TrackId, request.OrderId, System.Text.Json.JsonSerializer.Serialize(new { request.OrderId, request.TrackId, request.Status }),
            verification.OrderId ?? attempt.OrderId, attempt.Id);
        await commerceDbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
