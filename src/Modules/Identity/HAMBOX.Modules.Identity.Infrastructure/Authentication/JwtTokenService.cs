using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Identity.Application.Abstractions;
using HAMBOX.Modules.Identity.Application.Authorization;
using HAMBOX.Modules.Identity.Application.Options;
using HAMBOX.Modules.Identity.Domain.Users;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace HAMBOX.Modules.Identity.Infrastructure.Authentication;

/// <summary>
/// Service to generate JWT access tokens.
/// </summary>
internal sealed class JwtTokenService(
    IOptions<JwtSettings> jwtSettings,
    IPlatformSettingsProvider platformSettings) : IJwtTokenService
{
    private readonly JwtSettings _settings = jwtSettings.Value;

    /// <inheritdoc />
    public (string Token, DateTimeOffset ExpiresAt) GenerateAccessToken(
        ApplicationUser user,
        IEnumerable<Claim> claims)
    {
        var auth = platformSettings.GetAuthenticationAsync().GetAwaiter().GetResult();
        var expirationMinutes = auth.SessionTimeoutMinutes > 0
            ? auth.SessionTimeoutMinutes
            : _settings.AccessTokenExpirationMinutes;
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(expirationMinutes);

        var tokenClaims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("firstName", user.FirstName),
            new("lastName", user.LastName),
            new(IdentityClaimTypes.SecurityStamp, user.SecurityStamp)
        };

        tokenClaims.AddRange(claims);

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.SecretKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: tokenClaims,
            expires: expiresAt.UtcDateTime,
            signingCredentials: creds);

        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

        return (tokenString, expiresAt);
    }
}
