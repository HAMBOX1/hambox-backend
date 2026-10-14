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
            PaymentWebhookLogRecorder.Add(
                commerceDbContext, "Dot", "NotApplicable", "OrderNotFound",
                request.DotTransId, request.PartnerTransId, System.Text.Json.JsonSerializer.Serialize(new { request.DotTransId, request.PartnerTransId, request.OpId, Msisdn = MsisdnMasker.Mask(request.Msisdn), request.Amount, request.ServiceId, request.ResultCode, request.ResultDesc }));
            await commerceDbContext.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }

        var wasResolved = attempt.Status is HAMBOX.Modules.Commerce.Domain.Enums.PaymentAttemptStatus.Succeeded
            or HAMBOX.Modules.Commerce.Domain.Enums.PaymentAttemptStatus.Failed
            or HAMBOX.Modules.Commerce.Domain.Enums.PaymentAttemptStatus.Expired;

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
        var alreadyResolved = wasResolved;
        var verification = await verificationService.VerifyAndFinalizeAsync(attempt.Id, cancellationToken);

        var processingResult = alreadyResolved ? "Duplicate" : verification.Outcome switch
        {
            DotVerificationOutcome.Succeeded => "Paid",
            DotVerificationOutcome.Failed => "Failed",
            DotVerificationOutcome.StillPending => "Pending",
            _ => "OrderNotFound",
        };
        PaymentWebhookLogRecorder.Add(
            commerceDbContext, "Dot", "NotApplicable", processingResult,
            request.DotTransId, request.PartnerTransId, System.Text.Json.JsonSerializer.Serialize(new { request.DotTransId, request.PartnerTransId, request.OpId, Msisdn = MsisdnMasker.Mask(request.Msisdn), request.Amount, request.ServiceId, request.ResultCode, request.ResultDesc }),
            verification.OrderId ?? attempt.OrderId, attempt.Id);
        await commerceDbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
