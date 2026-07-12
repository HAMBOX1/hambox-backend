using System.Security.Claims;
using HAMBOX.Modules.Identity.Application.Abstractions;
using HAMBOX.Modules.Identity.Application.Authorization;
using HAMBOX.Modules.Identity.Application.Contracts;
using HAMBOX.Modules.Identity.Application.Options;
using HAMBOX.Modules.Identity.Domain.Sessions;
using HAMBOX.Modules.Identity.Domain.Users;
using DomainRefreshToken = HAMBOX.Modules.Identity.Domain.Tokens.RefreshToken;
using HAMBOX.SharedKernel.Results;
using Microsoft.Extensions.Options;

namespace HAMBOX.Modules.Identity.Infrastructure.Services;

internal sealed class AuthTokenIssuer(
    IIdentityDbContext dbContext,
    IJwtTokenService jwtTokenService,
    ITokenGenerator tokenGenerator,
    IUserClaimsService userClaimsService,
    IClientInfoParser clientInfoParser,
    IOptions<JwtSettings> jwtSettings) : IAuthTokenIssuer
{
    public async Task<Result<AuthTokenResponse>> IssueAsync(
        ApplicationUser user,
        string authContext,
        bool otpVerified,
        string ipAddress,
        string userAgent,
        CancellationToken cancellationToken = default)
    {
        var (browserName, deviceName) = clientInfoParser.ParseUserAgent(userAgent);
        var session = UserSession.Create(
            user.Id,
            ipAddress,
            userAgent,
            authContext,
            browserName,
            deviceName);

        var roleAndPermissionClaims = await userClaimsService.GetClaimsAsync(user.Id, cancellationToken);
        var claims = new List<Claim>(roleAndPermissionClaims)
        {
            new(IdentityClaimTypes.AuthContext, authContext),
            new(IdentityClaimTypes.OtpVerified, otpVerified ? "true" : "false"),
            new(IdentityClaimTypes.SessionId, session.Id.ToString()),
        };

        var (accessToken, expiresAt) = jwtTokenService.GenerateAccessToken(user, claims);

        var refreshTokenValue = tokenGenerator.GenerateSecureToken();
        var refreshExpiresAt = DateTimeOffset.UtcNow.AddDays(jwtSettings.Value.RefreshTokenExpirationDays);
        var (refreshToken, _) = DomainRefreshToken.Issue(
            user.Id,
            refreshTokenValue,
            refreshExpiresAt,
            authContext,
            session.Id);

        session.LinkRefreshToken(refreshToken.Id);

        dbContext.RefreshTokens.Add(refreshToken);
        dbContext.UserSessions.Add(session);

        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(new AuthTokenResponse(accessToken, refreshTokenValue, expiresAt));
    }
}
