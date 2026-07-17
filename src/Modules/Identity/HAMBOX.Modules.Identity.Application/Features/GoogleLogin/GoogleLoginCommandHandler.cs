using HAMBOX.Modules.Identity.Application.Abstractions;
using HAMBOX.Modules.Identity.Application.Authorization;
using HAMBOX.Modules.Identity.Application.Contracts;
using HAMBOX.Modules.Identity.Application.Errors;
using HAMBOX.Modules.Identity.Domain.Enums;
using HAMBOX.Modules.Identity.Domain.Sessions;
using HAMBOX.Modules.Identity.Domain.Users;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Identity.Application.Features.GoogleLogin;

/// <summary>
/// Handler for the <see cref="GoogleLoginCommand"/> command.
/// </summary>
internal sealed class GoogleLoginCommandHandler(
    IIdentityDbContext dbContext,
    IGoogleTokenValidator googleTokenValidator,
    IPasswordHasher passwordHasher,
    ITokenGenerator tokenGenerator,
    IAdminAccessResolver adminAccessResolver,
    IAuthTokenIssuer authTokenIssuer) : IRequestHandler<GoogleLoginCommand, Result<AuthTokenResponse>>
{
    /// <inheritdoc />
    public async Task<Result<AuthTokenResponse>> Handle(GoogleLoginCommand request, CancellationToken cancellationToken)
    {
        var payload = await googleTokenValidator.ValidateAsync(request.IdToken, cancellationToken);
        if (payload is null || !payload.EmailVerified)
        {
            return Result.Failure<AuthTokenResponse>(IdentityErrors.GoogleTokenInvalid);
        }

        var normalizedEmail = payload.Email.ToUpperInvariant();
        var user = await dbContext.Users
            .FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail, cancellationToken);

        if (user is null)
        {
            user = await CreateUserAsync(payload, cancellationToken);
            if (user is null)
            {
                return Result.Failure<AuthTokenResponse>(IdentityErrors.DefaultRoleNotFound);
            }
        }
        else
        {
            var linkResult = await LinkExistingUserAsync(user, request, cancellationToken);
            if (linkResult.IsFailure)
            {
                return Result.Failure<AuthTokenResponse>(linkResult.Error);
            }
        }

        user.ResetAccessFailedCount();
        dbContext.LoginHistory.Add(LoginHistory.RecordSuccess(user.Id, request.IpAddress, request.UserAgent));
        await dbContext.SaveChangesAsync(cancellationToken);

        return await authTokenIssuer.IssueAsync(
            user,
            AuthContextTypes.Customer,
            otpVerified: false,
            request.IpAddress,
            request.UserAgent,
            cancellationToken);
    }

    private async Task<ApplicationUser?> CreateUserAsync(GoogleTokenPayload payload, CancellationToken cancellationToken)
    {
        var customerRole = await dbContext.Roles
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.IsDefault, cancellationToken);

        if (customerRole is null)
        {
            return null;
        }

        var randomPasswordHash = passwordHasher.HashPassword(tokenGenerator.GenerateSecureToken());
        var user = ApplicationUser.Create(
            payload.Email,
            randomPasswordHash,
            payload.GivenName ?? "Google",
            payload.FamilyName ?? "User");

        user.ConfirmEmail();
        user.Activate();

        dbContext.Users.Add(user);
        dbContext.UserRoles.Add(UserRole.Create(user.Id, customerRole.Id));

        return user;
    }

    private async Task<Result> LinkExistingUserAsync(
        ApplicationUser user,
        GoogleLoginCommand request,
        CancellationToken cancellationToken)
    {
        if (user.LockoutEnd.HasValue && user.LockoutEnd > DateTimeOffset.UtcNow)
        {
            dbContext.LoginHistory.Add(LoginHistory.RecordFailure(user.Id, request.IpAddress, request.UserAgent, "Account locked"));
            await dbContext.SaveChangesAsync(cancellationToken);
            return Result.Failure(IdentityErrors.AccountLocked);
        }

        if (user.Status == UserStatus.Pending)
        {
            if (!user.EmailConfirmed)
            {
                user.ConfirmEmail();
            }

            user.Activate();

            var customerRole = await dbContext.Roles
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.IsDefault, cancellationToken);

            if (customerRole is not null)
            {
                var hasRole = await dbContext.UserRoles
                    .AnyAsync(ur => ur.UserId == user.Id && ur.RoleId == customerRole.Id, cancellationToken);

                if (!hasRole)
                {
                    dbContext.UserRoles.Add(UserRole.Create(user.Id, customerRole.Id));
                }
            }
        }
        else if (user.Status != UserStatus.Active)
        {
            dbContext.LoginHistory.Add(LoginHistory.RecordFailure(user.Id, request.IpAddress, request.UserAgent, "Account not active"));
            await dbContext.SaveChangesAsync(cancellationToken);
            return Result.Failure(IdentityErrors.AccountNotActive);
        }

        if (await adminAccessResolver.HasAdminPortalAccessAsync(user.Id, cancellationToken))
        {
            dbContext.LoginHistory.Add(LoginHistory.RecordFailure(
                user.Id,
                request.IpAddress,
                request.UserAgent,
                "Admin account must use admin portal login"));
            await dbContext.SaveChangesAsync(cancellationToken);
            return Result.Failure(IdentityErrors.AdminMustUseAdminPortal);
        }

        return Result.Success();
    }
}
