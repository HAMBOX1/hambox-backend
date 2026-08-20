using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Services;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HAMBOX.Modules.Commerce.Application.Features.Checkout.Dot;

internal sealed class HandleDotNotificationCommandHandler(
    ICommerceDbContext commerceDbContext,
    DotPaymentVerificationService verificationService,
    ILogger<HandleDotNotificationCommandHandler> logger)
    : IRequestHandler<HandleDotNotificationCommand, Result>
{
    public async Task<Result> Handle(HandleDotNotificationCommand request, CancellationToken cancellationToken)
    {
        var attempt = await commerceDbContext.PaymentAttempts
            .FirstOrDefaultAsync(p => p.Provider == "Dot" && p.PartnerTxId == request.PartnerTransId, cancellationToken);

        if (attempt is null)
        {
            // Nothing HAMBOX can reconcile this against. Ack anyway (return success so the
            // endpoint responds "1") — DOT retrying an unresolvable partner_txid all day helps no
            // one; log it so it's visible for investigation.
            logger.LogWarning("DOT notification for unknown partner_txid.");
            return Result.Success();
        }

        attempt.RecordProviderContext(
            request.DotTransId,
            MsisdnMasker.Mask(request.Msisdn),
            request.ResultCode,
            request.ResultDesc);

        await commerceDbContext.SaveChangesAsync(cancellationToken);

        // The notification body's resultCode is never trusted by itself — this call re-verifies
        // with DOT server-to-server. Safe to call repeatedly: DOT resends if the endpoint returns
        // anything other than "1", and the verification service's guarded claim makes every
        // repeat a no-op once the attempt is resolved.
        await verificationService.VerifyAndFinalizeAsync(attempt.Id, cancellationToken);

        return Result.Success();
    }
}
