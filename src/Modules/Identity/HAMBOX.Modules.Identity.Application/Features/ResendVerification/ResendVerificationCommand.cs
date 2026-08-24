using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Identity.Application.Features.ResendVerification;

/// <summary>
/// Command to resend an email verification message.
/// </summary>
/// <param name="Email">The account email address.</param>
public sealed record ResendVerificationCommand(string Email) : IRequest<Result>;
