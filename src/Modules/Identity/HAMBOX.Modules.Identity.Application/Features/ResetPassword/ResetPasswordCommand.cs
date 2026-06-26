using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Identity.Application.Features.ResetPassword;

/// <summary>
/// Command to reset a user's password using a valid reset token.
/// </summary>
/// <param name="Token">The password reset token.</param>
/// <param name="NewPassword">The new password.</param>
public sealed record ResetPasswordCommand(string Token, string NewPassword) : IRequest<Result>;
