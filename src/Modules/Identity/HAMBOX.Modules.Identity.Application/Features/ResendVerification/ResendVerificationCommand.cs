using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Identity.Application.Features.ResendVerification;

/// <summary>
/// Command to resend an email verification message.
/// </summary>
/// <param name="Email">The account email address.</param>
/// <param name="IpAddress">The client IP address, forwarded to Turnstile for verification.</param>
/// <param name="TurnstileToken">Cloudflare Turnstile response token, verified by the validator.</param>
/// <param name="UserAgent">The client user agent, for audit-trail purposes.</param>
/// <param name="CorrelationId">The request correlation ID, for audit-trail cross-referencing.</param>
public sealed record ResendVerificationCommand(
    string Email,
    string IpAddress,
    string TurnstileToken,
    string? UserAgent = null,
    string? CorrelationId = null) : IRequest<Result>;
