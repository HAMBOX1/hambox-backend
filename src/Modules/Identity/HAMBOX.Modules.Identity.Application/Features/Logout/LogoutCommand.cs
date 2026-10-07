using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Identity.Application.Features.Logout;

/// <summary>
/// Command to log out a user by revoking their refresh token.
/// </summary>
/// <param name="RefreshToken">The refresh token to revoke.</param>
/// <param name="IpAddress">The client IP address, for the security event audit entry.</param>
public sealed record LogoutCommand(string RefreshToken, string? IpAddress = null) : IRequest<Result>;
