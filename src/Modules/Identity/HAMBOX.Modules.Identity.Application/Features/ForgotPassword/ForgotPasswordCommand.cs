using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Identity.Application.Features.ForgotPassword;

/// <summary>
/// Command to request a password reset email for a user.
/// </summary>
/// <param name="Email">The user's email address.</param>
/// <param name="IpAddress">The client IP address, forwarded to Turnstile for verification.</param>
/// <param name="TurnstileToken">Cloudflare Turnstile response token, verified by the validator.</param>
/// <param name="UserAgent">The client user agent, for audit-trail purposes.</param>
/// <param name="CorrelationId">The request correlation ID, for audit-trail cross-referencing.</param>
public sealed record ForgotPasswordCommand(
    string Email,
    string IpAddress,
    string TurnstileToken,
    string? UserAgent = null,
    string? CorrelationId = null) : IRequest<Result>;
