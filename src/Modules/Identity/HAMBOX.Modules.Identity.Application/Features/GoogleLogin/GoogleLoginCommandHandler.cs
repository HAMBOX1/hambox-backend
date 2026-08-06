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
/// Handler for the <see cref="GoogleLoginCommand"/> command. Routes through the same
/// blocklist/device-block/trusted-device/risk-scoring/login-history pipeline as
/// <c>LoginCommandHandler</c> (password login) — Google's own token verification replaces the
/// password check, everything else is the shared security pipeline.
/// </summary>
internal sealed class GoogleLoginCommandHandler(
    IIdentityDbContext dbContext,
    IGoogleTokenValidator googleTokenValidator,
    IPasswordHasher passwordHasher,
    ITokenGenerator tokenGenerator,
    IAdminAccessResolver adminAccessResolver,
    IAuthTokenIssuer authTokenIssuer,
    ISecurityBlocklistService blocklistService,
    ISecurityEventLogger securityEventLogger,
    IClientInfoParser clientInfoParser,
    ITrustedDeviceService trustedDeviceService,
    ILoginRiskScorer riskScorer) : IRequestHandler<GoogleLoginCommand, Result<AuthTokenResponse>>
{
    /// <inheritdoc />
    public async Task<Result<AuthTokenResponse>> Handle(GoogleLoginCommand request, CancellationToken cancellationToken)
    {
        var payload = await googleTokenValidator.ValidateAsync(request.IdToken, cancellationToken);
        if (payload is null || !payload.EmailVerified)
        {
            return Result.Failure<AuthTokenResponse>(IdentityErrors.GoogleTokenInvalid);
        }

        var (browserName, osName, deviceType) = clientInfoParser.ParseUserAgent(request.UserAgent);
        var fingerprint = DeviceFingerprint.Compute(request.UserAgent);
        var context = new LoginContext(request.CountryCode, request.City, browserName, osName, deviceType, fingerprint);

        var normalizedEmail = payload.Email.ToUpperInvariant();
        var user = await dbContext.Users
            .FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail, cancellationToken);

        if (user is null)
        {
            // First-time sign-in via Google is equivalent to registration — apply the same
            // email-blocklist gate RegisterCommandHandler applies before creating an account.
            if (await blocklistService.IsEmailBlockedAsync(payload.Email, cancellationToken))
            {
                await securityEventLogger.LogAsync(
                    SecurityEventType.EmailBlock,
                    SecurityEventSeverity.Medium,
                    $"Google sign-up rejected for {payload.Email}: email address is blocked.",
                    ipAddress: request.IpAddress,
                    userAgent: request.UserAgent,
                    country: request.CountryCode,
                    city: request.City,
                    cancellationToken: cancellationToken);
                return Result.Failure<AuthTokenResponse>(IdentityErrors.RegistrationNotAllowed);
            }

            user = await CreateUserAsync(payload, cancellationToken);
            if (user is null)
            {
                return Result.Failure<AuthTokenResponse>(IdentityErrors.DefaultRoleNotFound);
            }
        }
        else
        {
            if (await blocklistService.IsEmailBlockedAsync(user.Email, cancellationToken))
            {
                var blockedFailure = LoginHistory.RecordFailure(
                    user.Id, request.IpAddress, request.UserAgent, "Email address is blocked", context, SecurityEventSeverity.High);
                dbContext.LoginHistory.Add(blockedFailure);
                await dbContext.SaveChangesAsync(cancellationToken);
                await securityEventLogger.LogAsync(
                    SecurityEventType.BlockedLogin,
                    SecurityEventSeverity.High,
                    $"Google login rejected for {user.Email}: email address is blocked.",
                    targetUserId: user.Id,
                    ipAddress: request.IpAddress,
                    userAgent: request.UserAgent,
                    country: request.CountryCode,
                    city: request.City,
                    cancellationToken: cancellationToken);
                return Result.Failure<AuthTokenResponse>(IdentityErrors.InvalidCredentials);
            }

            if (await trustedDeviceService.IsDeviceBlockedAsync(user.Id, fingerprint, cancellationToken))
            {
                var deviceBlockedFailure = LoginHistory.RecordFailure(
                    user.Id, request.IpAddress, request.UserAgent, "Device is blocked", context, SecurityEventSeverity.High);
                dbContext.LoginHistory.Add(deviceBlockedFailure);
                await dbContext.SaveChangesAsync(cancellationToken);
                await securityEventLogger.LogAsync(
                    SecurityEventType.DeviceBlock,
                    SecurityEventSeverity.High,
                    $"Google login rejected for {user.Email}: device is blocked.",
                    targetUserId: user.Id,
                    ipAddress: request.IpAddress,
                    userAgent: request.UserAgent,
                    country: request.CountryCode,
                    city: request.City,
                    cancellationToken: cancellationToken);
                return Result.Failure<AuthTokenResponse>(IdentityErrors.InvalidCredentials);
            }

            var linkResult = await LinkExistingUserAsync(user, request, context, cancellationToken);
            if (linkResult.IsFailure)
            {
                return Result.Failure<AuthTokenResponse>(linkResult.Error);
            }
        }

        user.ResetAccessFailedCount();

        var isNewCountry = !string.IsNullOrEmpty(request.CountryCode) && !await dbContext.LoginHistory.AnyAsync(
            h => h.UserId == user.Id && h.IsSuccessful && h.CountryCode == request.CountryCode, cancellationToken);
        var isNewDevice = await trustedDeviceService.RecordLoginAsync(user.Id, fingerprint, context, request.IpAddress, cancellationToken);
        var successRisk = riskScorer.ScoreSuccessfulLogin(isNewDevice, isNewCountry);

        var successHistory = LoginHistory.RecordSuccess(user.Id, request.IpAddress, request.UserAgent, context, successRisk);
        dbContext.LoginHistory.Add(successHistory);
        await dbContext.SaveChangesAsync(cancellationToken);

        return await authTokenIssuer.IssueAsync(
            user,
            AuthContextTypes.Customer,
            otpVerified: false,
            request.IpAddress,
            request.UserAgent,
            rememberMe: false,
            context,
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
        LoginContext context,
        CancellationToken cancellationToken)
    {
        if (user.LockoutEnd.HasValue && user.LockoutEnd > DateTimeOffset.UtcNow)
        {
            dbContext.LoginHistory.Add(LoginHistory.RecordFailure(user.Id, request.IpAddress, request.UserAgent, "Account locked", context));
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
            var isBlockedStatus = user.Status is UserStatus.Blocked or UserStatus.Banned or UserStatus.Suspended;
            dbContext.LoginHistory.Add(LoginHistory.RecordFailure(
                user.Id, request.IpAddress, request.UserAgent, "Account not active", context,
                isBlockedStatus ? SecurityEventSeverity.Medium : null));
            await dbContext.SaveChangesAsync(cancellationToken);

            if (isBlockedStatus)
            {
                await securityEventLogger.LogAsync(
                    SecurityEventType.BlockedLogin,
                    SecurityEventSeverity.Medium,
                    $"Google login rejected for {user.Email}: account is {user.Status}.",
                    targetUserId: user.Id,
                    ipAddress: request.IpAddress,
                    userAgent: request.UserAgent,
                    country: request.CountryCode,
                    city: request.City,
                    cancellationToken: cancellationToken);
            }

            return Result.Failure(IdentityErrors.AccountNotActive);
        }

        if (await adminAccessResolver.HasAdminPortalAccessAsync(user.Id, cancellationToken))
        {
            dbContext.LoginHistory.Add(LoginHistory.RecordFailure(
                user.Id,
                request.IpAddress,
                request.UserAgent,
                "Admin account must use admin portal login",
                context));
            await dbContext.SaveChangesAsync(cancellationToken);
            return Result.Failure(IdentityErrors.AdminMustUseAdminPortal);
        }

        return Result.Success();
    }
}
