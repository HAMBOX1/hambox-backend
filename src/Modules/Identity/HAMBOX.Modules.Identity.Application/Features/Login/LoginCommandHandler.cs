using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Identity.Application.Abstractions;
using HAMBOX.Modules.Identity.Application.Authorization;
using HAMBOX.Modules.Identity.Application.Contracts;
using HAMBOX.Modules.Identity.Application.Errors;
using HAMBOX.Modules.Identity.Application.Options;
using HAMBOX.Modules.Identity.Domain.Enums;
using HAMBOX.Modules.Identity.Domain.Sessions;
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
    IAdminAccessResolver adminAccessResolver,
    IAuthTokenIssuer authTokenIssuer,
    IPlatformSettingsProvider platformSettings) : IRequestHandler<LoginCommand, Result<AuthTokenResponse>>
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
            var security = await platformSettings.GetSecurityAsync(cancellationToken);
            user.RecordAccessFailure(
                security.MaxFailedAccessAttempts,
                TimeSpan.FromMinutes(security.LockoutDurationMinutes));

            var failure = LoginHistory.RecordFailure(user.Id, request.IpAddress, request.UserAgent, "Invalid credentials");
            dbContext.LoginHistory.Add(failure);
            await dbContext.SaveChangesAsync(cancellationToken);

            if (user.LockoutEnd.HasValue && user.LockoutEnd > DateTimeOffset.UtcNow)
            {
                return Result.Failure<AuthTokenResponse>(IdentityErrors.AccountLocked);
            }

            return Result.Failure<AuthTokenResponse>(IdentityErrors.InvalidCredentials);
        }

        if (await adminAccessResolver.HasAdminPortalAccessAsync(user.Id, cancellationToken))
        {
            var adminFailure = LoginHistory.RecordFailure(
                user.Id,
                request.IpAddress,
                request.UserAgent,
                "Admin account must use admin portal login");
            dbContext.LoginHistory.Add(adminFailure);
            await dbContext.SaveChangesAsync(cancellationToken);
            return Result.Failure<AuthTokenResponse>(IdentityErrors.AdminMustUseAdminPortal);
        }

        user.ResetAccessFailedCount();

        var successHistory = LoginHistory.RecordSuccess(user.Id, request.IpAddress, request.UserAgent);
        dbContext.LoginHistory.Add(successHistory);
        await dbContext.SaveChangesAsync(cancellationToken);

        return await authTokenIssuer.IssueAsync(
            user,
            AuthContextTypes.Customer,
            otpVerified: false,
            request.IpAddress,
            request.UserAgent,
            cancellationToken);
    }
}
