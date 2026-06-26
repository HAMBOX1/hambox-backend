using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Identity.Application.Features.VerifyEmail;

/// <summary>
/// Command to verify a user's email address using a verification token.
/// </summary>
/// <param name="Token">The email verification token value.</param>
public sealed record VerifyEmailCommand(string Token) : IRequest<Result>;
