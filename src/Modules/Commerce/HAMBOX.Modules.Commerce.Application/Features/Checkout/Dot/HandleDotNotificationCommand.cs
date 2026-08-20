using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Commerce.Application.Features.Checkout.Dot;

/// <summary>
/// Handles DOT's server-to-server Transaction Status Notification webhook. Like the browser
/// callback, nothing in this payload is trusted to determine payment success on its own — the
/// documented API has no signature or shared secret, only an IP-allowlist recommendation (enforced
/// at the endpoint, defense-in-depth only). This command's only job is to locate the matching
/// <c>PaymentAttempt</c> by <paramref name="PartnerTransId"/> and trigger the same authoritative
/// verification call the browser callback and reconciliation sweep use — the notification body's
/// own <c>resultCode</c> never flips anything by itself.
/// </summary>
public sealed record HandleDotNotificationCommand(
    string? DotTransId,
    string? PartnerTransId,
    int? OpId,
    string? Msisdn,
    string? Amount,
    string? ServiceId,
    string? ResultCode,
    string? ResultDesc) : IRequest<Result>;
