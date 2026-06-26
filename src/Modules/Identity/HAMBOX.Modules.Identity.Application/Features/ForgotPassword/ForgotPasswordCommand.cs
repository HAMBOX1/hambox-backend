using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Identity.Application.Features.ForgotPassword;

/// <summary>
/// Command to request a password reset email for a user.
/// </summary>
/// <param name="Email">The user's email address.</param>
public sealed record ForgotPasswordCommand(string Email) : IRequest<Result>;
