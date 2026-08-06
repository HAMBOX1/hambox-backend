using System.Security.Claims;
using HAMBOX.Application.Abstractions;
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
    IPlatformSettingsProvider platformSettings,
    IOptions<JwtSettings> jwtSettings) : IAuthTokenIssuer
{
    public async Task<Result<AuthTokenResponse>> IssueAsync(
        ApplicationUser user,
        string authContext,
        bool otpVerified,
        string ipAddress,
        string userAgent,
        bool rememberMe = false,
        LoginContext? context = null,
        CancellationToken cancellationToken = default)
    {
        var session = UserSession.Create(
            user.Id,
            ipAddress,
            userAgent,
            authContext,
            context);

        var roleAndPermissionClaims = await userClaimsService.GetClaimsAsync(user.Id, cancellationToken);
        var claims = new List<Claim>(roleAndPermissionClaims)
        {
            new(IdentityClaimTypes.AuthContext, authContext),
            new(IdentityClaimTypes.OtpVerified, otpVerified ? "true" : "false"),
            new(IdentityClaimTypes.SessionId, session.Id.ToString()),
        };

        var (accessToken, expiresAt) = jwtTokenService.GenerateAccessToken(user, claims);

        var refreshTokenValue = tokenGenerator.GenerateSecureToken();
        var refreshExpiresAt = await ResolveRefreshExpirationAsync(rememberMe, cancellationToken);
        var (refreshToken, _) = DomainRefreshToken.Issue(
            user.Id,
            refreshTokenValue,
            refreshExpiresAt,
            authContext,
            session.Id,
            rememberMe);

        session.LinkRefreshToken(refreshToken.Id);

        dbContext.RefreshTokens.Add(refreshToken);
        dbContext.UserSessions.Add(session);

        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(new AuthTokenResponse(accessToken, refreshTokenValue, expiresAt));
    }

    private async Task<DateTimeOffset> ResolveRefreshExpirationAsync(bool rememberMe, CancellationToken cancellationToken)
    {
        if (!rememberMe)
        {
            return DateTimeOffset.UtcNow.AddDays(jwtSettings.Value.RefreshTokenExpirationDays);
        }

        var auth = await platformSettings.GetAuthenticationAsync(cancellationToken);
        var rememberMeDays = auth.RememberMeDurationDays > 0
            ? auth.RememberMeDurationDays
            : jwtSettings.Value.RefreshTokenExpirationDays;

        return DateTimeOffset.UtcNow.AddDays(rememberMeDays);
    }
}
