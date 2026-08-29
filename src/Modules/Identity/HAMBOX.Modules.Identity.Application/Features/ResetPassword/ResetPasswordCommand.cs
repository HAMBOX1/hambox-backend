using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Identity.Application.Features.ResetPassword;

/// <summary>
/// Command to reset a user's password using a valid reset token.
/// </summary>
/// <param name="Token">The password reset token.</param>
/// <param name="NewPassword">The new password.</param>
/// <param name="IpAddress">The client IP address, for audit-trail purposes.</param>
/// <param name="UserAgent">The client user agent, for audit-trail purposes.</param>
/// <param name="CorrelationId">The request correlation ID, for audit-trail cross-referencing.</param>
public sealed record ResetPasswordCommand(
    string Token,
    string NewPassword,
    string? IpAddress = null,
    string? UserAgent = null,
    string? CorrelationId = null) : IRequest<Result>;
