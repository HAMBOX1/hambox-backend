using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Identity.Application.Features.ForgotPassword;

/// <summary>
/// Command to request a password reset email for a user.
/// </summary>
/// <param name="Email">The user's email address.</param>
/// <param name="IpAddress">The client IP address, forwarded to Turnstile for verification.</param>
/// <param name="TurnstileToken">Cloudflare Turnstile response token, verified by the validator.</param>
public sealed record ForgotPasswordCommand(string Email, string IpAddress, string TurnstileToken) : IRequest<Result>;
