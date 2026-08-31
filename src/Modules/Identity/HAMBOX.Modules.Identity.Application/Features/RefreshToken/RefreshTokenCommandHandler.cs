using System.Security.Claims;
using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Identity.Application.Abstractions;
using HAMBOX.Modules.Identity.Application.Authorization;
using HAMBOX.Modules.Identity.Application.Contracts;
using HAMBOX.Modules.Identity.Application.Errors;
using HAMBOX.Modules.Identity.Application.Options;
using HAMBOX.Modules.Identity.Domain.Enums;
using HAMBOX.Modules.Identity.Domain.Sessions;
using DomainRefreshToken = HAMBOX.Modules.Identity.Domain.Tokens.RefreshToken;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HAMBOX.Modules.Identity.Application.Features.RefreshToken;

/// <summary>
/// Handler for the <see cref="RefreshTokenCommand"/> command.
/// </summary>
internal sealed class RefreshTokenCommandHandler(
    IIdentityDbContext dbContext,
    IJwtTokenService jwtTokenService,
    ITokenGenerator tokenGenerator,
    IUserClaimsService userClaimsService,
    IPlatformSettingsProvider platformSettings,
    IOptions<JwtSettings> jwtSettings) : IRequestHandler<RefreshTokenCommand, Result<AuthTokenResponse>>
{
    /// <inheritdoc />
    public async Task<Result<AuthTokenResponse>> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        var tokenHash = DomainRefreshToken.GetLookupHash(request.RefreshToken);
        var existingToken = await dbContext.RefreshTokens
            .FirstOrDefaultAsync(t => t.Token == tokenHash, cancellationToken);

        if (existingToken is null)
        {
            return Result.Failure<AuthTokenResponse>(IdentityErrors.InvalidToken);
        }

        if (existingToken.WasReused())
        {
            await RevokeAllUserTokensAsync(existingToken.UserId, cancellationToken);
            return Result.Failure<AuthTokenResponse>(IdentityErrors.InvalidToken);
        }

        if (!existingToken.IsActive)
        {
            return Result.Failure<AuthTokenResponse>(IdentityErrors.InvalidToken);
        }

        var user = await dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == existingToken.UserId, cancellationToken);

        if (user is null)
        {
            return Result.Failure<AuthTokenResponse>(IdentityErrors.UserNotFound);
        }

        if (user.Status != UserStatus.Active)
        {
            return Result.Failure<AuthTokenResponse>(IdentityErrors.AccountNotActive);
        }

        var session = await dbContext.UserSessions
            .FirstOrDefaultAsync(s => s.Id == existingToken.SessionId, cancellationToken);

        if (session is null || !session.IsActive)
        {
            return Result.Failure<AuthTokenResponse>(IdentityErrors.InvalidToken);
        }

        var newRefreshTokenValue = tokenGenerator.GenerateSecureToken();
        var refreshExpiresAt = await ResolveRefreshExpirationAsync(existingToken.IsPersistent, cancellationToken);
        var (newRefreshToken, _) = DomainRefreshToken.Issue(
            user.Id,
            newRefreshTokenValue,
            refreshExpiresAt,
            existingToken.AuthContext,
            existingToken.SessionId,
            existingToken.IsPersistent);

        existingToken.MarkReplacedBy(newRefreshTokenValue);
        existingToken.Revoke();

        var roleAndPermissionClaims = await userClaimsService.GetClaimsAsync(user.Id, cancellationToken);
        var otpVerified = existingToken.AuthContext == AuthContextTypes.Admin;
        var claims = new List<Claim>(roleAndPermissionClaims)
        {
            new(IdentityClaimTypes.AuthContext, existingToken.AuthContext),
            new(IdentityClaimTypes.OtpVerified, otpVerified ? "true" : "false"),
            new(IdentityClaimTypes.SessionId, existingToken.SessionId.ToString()),
        };

        var (accessToken, expiresAt) = await jwtTokenService.GenerateAccessTokenAsync(user, claims, cancellationToken);

        session.LinkRefreshToken(newRefreshToken.Id);
        session.RecordActivity();

        dbContext.RefreshTokens.Add(newRefreshToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(new AuthTokenResponse(accessToken, newRefreshTokenValue, expiresAt, refreshExpiresAt));
    }

    private async Task RevokeAllUserTokensAsync(Guid userId, CancellationToken cancellationToken)
    {
        var tokens = await dbContext.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedOnUtc == null)
            .ToListAsync(cancellationToken);

        foreach (var token in tokens)
        {
            if (!token.IsRevoked)
            {
                token.Revoke();
            }
        }

        var sessions = await dbContext.UserSessions
            .Where(s => s.UserId == userId && s.EndedOnUtc == null)
            .ToListAsync(cancellationToken);

        foreach (var session in sessions)
        {
            session.End();
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<DateTimeOffset> ResolveRefreshExpirationAsync(bool isPersistent, CancellationToken cancellationToken)
    {
        if (!isPersistent)
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
