using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Commerce.Application.Features.Checkout.OxaPay;

/// <summary>
/// Handles OxaPay's server-to-server payment-status webhook. Even though the endpoint checks the
/// request's <c>HMAC</c> header before this command ever runs (see
/// <c>OxaPayWebhookSignatureVerifier</c>), the webhook body's own <c>status</c> field still isn't
/// what flips the order — this command's only job is to locate the matching <c>PaymentAttempt</c> by
/// <paramref name="OrderId"/> and trigger the same authoritative OxaPay Payment-Information call the
/// active-poll re-verification uses, mirroring <c>Cryptomus.HandleCryptomusWebhookCommand</c>'s
/// "never trust the callback alone" stance.
/// </summary>
public sealed record HandleOxaPayWebhookCommand(
    string? OrderId,
    string? TrackId,
    string? Status) : IRequest<Result>;
