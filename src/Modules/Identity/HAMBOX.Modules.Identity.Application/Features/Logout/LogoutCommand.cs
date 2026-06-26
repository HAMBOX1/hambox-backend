using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Identity.Application.Features.Logout;

/// <summary>
/// Command to log out a user by revoking their refresh token.
/// </summary>
/// <param name="RefreshToken">The refresh token to revoke.</param>
public sealed record LogoutCommand(string RefreshToken) : IRequest<Result>;
