using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Commerce.Application.Features.Checkout.Dot;

/// <summary>
/// Handles the customer's browser landing back on HAMBOX after the DOT OTP flow (the <c>rurl</c>
/// redirect). This is deliberately never trusted to determine payment success by itself — every
/// field here (including <c>reason_code</c> and <c>amount</c>) is browser-supplied and unsigned per
/// the DOT documentation (the <c>signature</c> field's algorithm is undocumented, so it cannot be
/// validated). The only thing this command does with these values is look up the matching
/// <c>PaymentAttempt</c> by <paramref name="PartnerTxId"/>, sanity-check the operator/service
/// context, record them for audit, and then trigger the same authoritative
/// <c>DotPaymentVerificationService</c> call every other trigger (webhook, reconciliation) uses.
/// </summary>
public sealed record HandleDotRedirectCallbackCommand(
    string? PartnerTxId,
    string? DotTxId,
    int? OpId,
    string? ServiceId,
    string? Msisdn,
    string? ReasonCode,
    string? ReasonDesc) : IRequest<Result<Guid>>;
