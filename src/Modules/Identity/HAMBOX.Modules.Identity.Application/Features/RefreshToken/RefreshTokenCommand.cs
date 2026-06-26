using HAMBOX.Modules.Identity.Application.Contracts;
using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Identity.Application.Features.RefreshToken;

/// <summary>
/// Command to refresh authentication tokens using a valid refresh token.
/// </summary>
/// <param name="RefreshToken">The active refresh token.</param>
public sealed record RefreshTokenCommand(string RefreshToken) : IRequest<Result<AuthTokenResponse>>;
