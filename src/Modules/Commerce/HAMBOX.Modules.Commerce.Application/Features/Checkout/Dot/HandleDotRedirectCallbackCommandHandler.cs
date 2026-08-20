using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Errors;
using HAMBOX.Modules.Commerce.Application.Options;
using HAMBOX.Modules.Commerce.Application.Services;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HAMBOX.Modules.Commerce.Application.Features.Checkout.Dot;

internal sealed class HandleDotRedirectCallbackCommandHandler(
    ICommerceDbContext commerceDbContext,
    DotPaymentVerificationService verificationService,
    IOptions<DotSettings> dotOptions,
    ILogger<HandleDotRedirectCallbackCommandHandler> logger)
    : IRequestHandler<HandleDotRedirectCallbackCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(HandleDotRedirectCallbackCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.PartnerTxId))
        {
            return Result.Failure<Guid>(CommerceErrors.DotCallbackInvalid);
        }

        var attempt = await commerceDbContext.PaymentAttempts
            .FirstOrDefaultAsync(p => p.Provider == "Dot" && p.PartnerTxId == request.PartnerTxId, cancellationToken);

        if (attempt is null)
        {
            logger.LogWarning("DOT redirect callback for unknown partner_txid.");
            return Result.Failure<Guid>(CommerceErrors.DotCallbackInvalid);
        }

        var settings = dotOptions.Value;
        var opIdMatches = request.OpId is null || string.Equals(request.OpId.Value.ToString(), attempt.OperatorId, StringComparison.Ordinal);
        var serviceIdMatches = string.IsNullOrWhiteSpace(request.ServiceId) || string.Equals(request.ServiceId, attempt.ServiceId, StringComparison.Ordinal);

        if (!opIdMatches || !serviceIdMatches || !string.Equals(settings.OperatorId, attempt.OperatorId, StringComparison.Ordinal))
        {
            // Doesn't match the operator/service context this attempt was actually initiated
            // under. Never let a mismatched callback trigger verification of a different attempt —
            // just record nothing new and let the customer poll status normally.
            logger.LogWarning(
                "DOT redirect callback operator/service context mismatch for payment attempt {PaymentAttemptId}.",
                attempt.Id);
            return Result.Failure<Guid>(CommerceErrors.DotCallbackInvalid);
        }

        attempt.RecordProviderContext(
            request.DotTxId,
            MsisdnMasker.Mask(request.Msisdn),
            request.ReasonCode,
            request.ReasonDesc);

        await commerceDbContext.SaveChangesAsync(cancellationToken);

        // Never trust reason_code alone — always re-verify with DOT server-to-server before this
        // attempt's status changes at all.
        await verificationService.VerifyAndFinalizeAsync(attempt.Id, cancellationToken);

        return Result.Success(attempt.Id);
    }
}
