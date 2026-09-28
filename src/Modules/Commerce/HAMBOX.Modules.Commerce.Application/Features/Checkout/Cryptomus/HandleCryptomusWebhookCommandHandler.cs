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
            return Result.Success();
        }

        attempt.RecordProviderContext(request.Uuid, null, request.Status, null);
        await commerceDbContext.SaveChangesAsync(cancellationToken);

        // The webhook body's status is never trusted by itself — this re-verifies with Cryptomus
        // server-to-server. Safe to call repeatedly (idempotent claim-guarded).
        await verificationService.VerifyAndFinalizeAsync(attempt.Id, cancellationToken);

        return Result.Success();
    }
}
