using HAMBOX.Modules.Identity.Application.Abstractions;
using HAMBOX.Modules.Identity.Application.Contracts;
using HAMBOX.Modules.Identity.Application.Errors;
using HAMBOX.Modules.Identity.Domain.Enums;
using HAMBOX.Modules.Identity.Domain.Sessions;
using DomainRefreshToken = HAMBOX.Modules.Identity.Domain.Tokens.RefreshToken;
using HAMBOX.Modules.Identity.Application.Options;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HAMBOX.Modules.Identity.Application.Features.Login;

/// <summary>
/// Handler for the <see cref="LoginCommand"/> command.
/// </summary>
internal sealed class LoginCommandHandler(
    IIdentityDbContext dbContext,
    IPasswordHasher passwordHasher,
    IJwtTokenService jwtTokenService,
    ITokenGenerator tokenGenerator,
    IUserClaimsService userClaimsService,
    IOptions<JwtSettings> jwtSettings,
    IOptions<LockoutSettings> lockoutSettings) : IRequestHandler<LoginCommand, Result<AuthTokenResponse>>
{
    /// <inheritdoc />
    public async Task<Result<AuthTokenResponse>> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var normalizedEmail = request.Email.ToUpperInvariant();
        var user = await dbContext.Users
            .FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail, cancellationToken);

        if (user is null)
        {
            return Result.Failure<AuthTokenResponse>(IdentityErrors.InvalidCredentials);
        }

        if (!user.EmailConfirmed)
        {
            var failure = LoginHistory.RecordFailure(user.Id, request.IpAddress, request.UserAgent, "Email not confirmed");
            dbContext.LoginHistory.Add(failure);
            await dbContext.SaveChangesAsync(cancellationToken);
            return Result.Failure<AuthTokenResponse>(IdentityErrors.EmailNotConfirmed);
        }

        if (user.Status != UserStatus.Active)
        {
            var failure = LoginHistory.RecordFailure(user.Id, request.IpAddress, request.UserAgent, "Account not active");
            dbContext.LoginHistory.Add(failure);
            await dbContext.SaveChangesAsync(cancellationToken);
            return Result.Failure<AuthTokenResponse>(IdentityErrors.AccountNotActive);
        }

        if (user.LockoutEnd.HasValue && user.LockoutEnd > DateTimeOffset.UtcNow)
        {
            var failure = LoginHistory.RecordFailure(user.Id, request.IpAddress, request.UserAgent, "Account locked");
            dbContext.LoginHistory.Add(failure);
            await dbContext.SaveChangesAsync(cancellationToken);
            return Result.Failure<AuthTokenResponse>(IdentityErrors.AccountLocked);
        }

        var isPasswordValid = passwordHasher.VerifyPassword(user.PasswordHash, request.Password);
        if (!isPasswordValid)
        {
            var lockout = lockoutSettings.Value;
            user.RecordAccessFailure(
                lockout.MaxFailedAccessAttempts,
                TimeSpan.FromMinutes(lockout.LockoutDurationMinutes));

            var failure = LoginHistory.RecordFailure(user.Id, request.IpAddress, request.UserAgent, "Invalid credentials");
            dbContext.LoginHistory.Add(failure);
            await dbContext.SaveChangesAsync(cancellationToken);

            if (user.LockoutEnd.HasValue && user.LockoutEnd > DateTimeOffset.UtcNow)
            {
                return Result.Failure<AuthTokenResponse>(IdentityErrors.AccountLocked);
            }

            return Result.Failure<AuthTokenResponse>(IdentityErrors.InvalidCredentials);
        }

        user.ResetAccessFailedCount();

        var claims = await userClaimsService.GetClaimsAsync(user.Id, cancellationToken);
        var (accessToken, expiresAt) = jwtTokenService.GenerateAccessToken(user, claims);
        var refreshTokenValue = tokenGenerator.GenerateSecureToken();
        var refreshExpiresAt = DateTimeOffset.UtcNow.AddDays(jwtSettings.Value.RefreshTokenExpirationDays);
        var (refreshToken, _) = DomainRefreshToken.Issue(user.Id, refreshTokenValue, refreshExpiresAt);

        var session = UserSession.Create(user.Id, request.IpAddress, request.UserAgent);
        var successHistory = LoginHistory.RecordSuccess(user.Id, request.IpAddress, request.UserAgent);

        dbContext.RefreshTokens.Add(refreshToken);
        dbContext.UserSessions.Add(session);
        dbContext.LoginHistory.Add(successHistory);

        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(new AuthTokenResponse(accessToken, refreshTokenValue, expiresAt));
    }
}
