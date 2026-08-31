using System.Security.Claims;
using HAMBOX.Modules.Identity.Domain.Users;
using HAMBOX.SharedKernel.Results;

namespace HAMBOX.Modules.Identity.Application.Abstractions;

/// <summary>
/// Generates and validates JWT access tokens.
/// </summary>
public interface IJwtTokenService
{
    /// <summary>
    /// Generates a JWT access token for the specified user with the provided claims.
    /// </summary>
    /// <param name="user">The user to generate the token for.</param>
    /// <param name="claims">The custom claims (roles and permissions) to include in the token.</param>
    /// <returns>A tuple containing the token string and its expiration date.</returns>
    Task<(string Token, DateTimeOffset ExpiresAt)> GenerateAccessTokenAsync(
        ApplicationUser user,
        IEnumerable<Claim> claims,
        CancellationToken cancellationToken = default);
}
