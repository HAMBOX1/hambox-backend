using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Services;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HAMBOX.Modules.Commerce.Application.Features.Checkout.Cryptomus;

internal sealed class HandleCryptomusWebhookCommandHandler(
    ICommerceDbContext commerceDbContext,
    CryptomusPaymentVerificationService verificationService,
    ILogger<HandleCryptomusWebhookCommandHandler> logger)
    : IRequestHandler<HandleCryptomusWebhookCommand, Result>
{
    public async Task<Result> Handle(HandleCryptomusWebhookCommand request, CancellationToken cancellationToken)
    {
        var attempt = await commerceDbContext.PaymentAttempts
            .FirstOrDefaultAsync(p => p.Provider == "Cryptomus" && p.PartnerTxId == request.OrderId, cancellationToken);

        if (attempt is null)
        {
            // Nothing HAMBOX can reconcile this against. Ack anyway — Cryptomus retrying an
            // unresolvable order_id helps no one; log it so it's visible for investigation.
            logger.LogWarning("Cryptomus webhook for unknown order_id.");
            PaymentWebhookLogRecorder.Add(
                commerceDbContext, "Cryptomus", "Valid", "OrderNotFound",
                request.Uuid, request.OrderId, System.Text.Json.JsonSerializer.Serialize(new { request.OrderId, request.Uuid, request.Status, request.IsFinal }));
            await commerceDbContext.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }

        var wasResolved = attempt.Status is HAMBOX.Modules.Commerce.Domain.Enums.PaymentAttemptStatus.Succeeded
            or HAMBOX.Modules.Commerce.Domain.Enums.PaymentAttemptStatus.Failed
            or HAMBOX.Modules.Commerce.Domain.Enums.PaymentAttemptStatus.Expired;

        attempt.RecordProviderContext(request.Uuid, null, request.Status, null);
        await commerceDbContext.SaveChangesAsync(cancellationToken);

        // The webhook body's status is never trusted by itself — this re-verifies with Cryptomus
        // server-to-server. Safe to call repeatedly (idempotent claim-guarded).
        var alreadyResolved = wasResolved;
        var verification = await verificationService.VerifyAndFinalizeAsync(attempt.Id, cancellationToken);

        var processingResult = alreadyResolved ? "Duplicate" : verification.Outcome switch
        {
            CryptomusVerificationOutcome.Succeeded => "Paid",
            CryptomusVerificationOutcome.Failed => "Failed",
            CryptomusVerificationOutcome.StillPending => "Pending",
            _ => "OrderNotFound",
        };
        PaymentWebhookLogRecorder.Add(
            commerceDbContext, "Cryptomus", "Valid", processingResult,
            request.Uuid, request.OrderId, System.Text.Json.JsonSerializer.Serialize(new { request.OrderId, request.Uuid, request.Status, request.IsFinal }),
            verification.OrderId ?? attempt.OrderId, attempt.Id);
        await commerceDbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
