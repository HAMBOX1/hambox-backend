using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Commerce.Application.Features.Checkout.DotFawry;

/// <summary>
/// Handles DOT's server-to-server Transaction Status Notification webhook for the Fawry Direct
/// Billing product ("Once the billing is successful, you will receive a notification callback
/// confirming the successful transaction" — confirmed by DOT). Like the sibling carrier-billing
/// integration, nothing in this payload is trusted to determine payment success on its own — the
/// documented API has no signature or shared secret, only an IP-allowlist recommendation (enforced
/// at the endpoint, defense-in-depth only). This command's only job is to locate the matching
/// <c>PaymentAttempt</c> by <paramref name="PartnerTransId"/> and trigger the same authoritative
/// verification call the reconciliation sweep uses — the notification body's own
/// <c>resultCode</c> never flips anything by itself.
/// </summary>
public sealed record HandleDotFawryNotificationCommand(
    string? DotTransId,
    string? PartnerTransId,
    int? OpId,
    string? Msisdn,
    string? Amount,
    string? ServiceId,
    string? ResultCode,
    string? ResultDesc) : IRequest<Result>;
