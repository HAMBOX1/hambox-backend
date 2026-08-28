using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Identity.Application.Features.ResendVerification;

/// <summary>
/// Command to resend an email verification message.
/// </summary>
/// <param name="Email">The account email address.</param>
/// <param name="IpAddress">The client IP address, forwarded to Turnstile for verification.</param>
/// <param name="TurnstileToken">Cloudflare Turnstile response token, verified by the validator.</param>
public sealed record ResendVerificationCommand(string Email, string IpAddress, string TurnstileToken) : IRequest<Result>;
