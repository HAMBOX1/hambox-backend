using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Identity.Application.Features.ResendVerification;

/// <summary>
/// Command to resend an email verification message.
/// </summary>
/// <param name="Email">The account email address.</param>
/// <param name="IpAddress">The client IP address, forwarded to Cloudflare Turnstile for verification.</param>
/// <param name="TurnstileToken">Cloudflare Turnstile token proving the client passed the security widget.</param>
public sealed record ResendVerificationCommand(string Email, string IpAddress, string TurnstileToken) : IRequest<Result>;
