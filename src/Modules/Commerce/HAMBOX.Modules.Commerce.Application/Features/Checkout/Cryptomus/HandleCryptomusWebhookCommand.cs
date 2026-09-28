using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Commerce.Application.Features.Checkout.Cryptomus;

/// <summary>
/// Handles Cryptomus's server-to-server payment-status webhook. Even though the endpoint checks the
/// request's <c>sign</c> header before this command ever runs (see
/// <c>CryptomusWebhookSignatureVerifier</c>), the webhook body's own <c>status</c> field still isn't
/// what flips the order — this command's only job is to locate the matching <c>PaymentAttempt</c> by
/// <paramref name="OrderId"/> and trigger the same authoritative Cryptomus Payment-Information call
/// the active-poll re-verification and reconciliation sweep use, mirroring
/// <c>Dot.HandleDotNotificationCommand</c>'s "never trust the callback alone" stance.
/// </summary>
public sealed record HandleCryptomusWebhookCommand(
    string? OrderId,
    string? Uuid,
    string? Status,
    bool? IsFinal) : IRequest<Result>;
