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
    IPlatformSettingsProvider platformSettings,
    ISecurityBlocklistService blocklistService,
    ISecurityEventLogger securityEventLogger,
    IClientInfoParser clientInfoParser,
    ITrustedDeviceService trustedDeviceService,
    ILoginRiskScorer riskScorer) : IRequestHandler<LoginCommand, Result<AuthTokenResponse>>
{
    /// <inheritdoc />
    public async Task<Result<AuthTokenResponse>> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var (browserName, osName, deviceType) = clientInfoParser.ParseUserAgent(request.UserAgent);
        var fingerprint = DeviceFingerprint.Compute(request.UserAgent);
        var context = new LoginContext(request.CountryCode, request.City, browserName, osName, deviceType, fingerprint);

        var normalizedEmail = request.Email.ToUpperInvariant();
        var user = await dbContext.Users
            .FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail, cancellationToken);

        if (user is null)
        {
            return Result.Failure<AuthTokenResponse>(IdentityErrors.InvalidCredentials);
        }

        if (await blocklistService.IsEmailBlockedAsync(user.Email, cancellationToken))
        {
            var blockedFailure = LoginHistory.RecordFailure(
                user.Id, request.IpAddress, request.UserAgent, "Email address is blocked", context, SecurityEventSeverity.High);
            dbContext.LoginHistory.Add(blockedFailure);
            await dbContext.SaveChangesAsync(cancellationToken);
            await securityEventLogger.LogAsync(
                SecurityEventType.BlockedLogin,
                SecurityEventSeverity.High,
                $"Login rejected for {user.Email}: email address is blocked.",
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
                $"Login rejected for {user.Email}: device is blocked.",
                targetUserId: user.Id,
                ipAddress: request.IpAddress,
                userAgent: request.UserAgent,
                country: request.CountryCode,
                city: request.City,
                cancellationToken: cancellationToken);
            return Result.Failure<AuthTokenResponse>(IdentityErrors.InvalidCredentials);
        }

        if (!user.EmailConfirmed)
        {
            var failure = LoginHistory.RecordFailure(user.Id, request.IpAddress, request.UserAgent, "Email not confirmed", context);
            dbContext.LoginHistory.Add(failure);
            await dbContext.SaveChangesAsync(cancellationToken);
            // Externally indistinguishable from a wrong password — distinguishing this from
            // InvalidCredentials would let an attacker enumerate registered emails without ever
            // guessing a password. Internal LoginHistory/audit trail above still records the real reason.
            return Result.Failure<AuthTokenResponse>(IdentityErrors.InvalidCredentials);
        }

        if (user.Status != UserStatus.Active)
        {
            var isBlockedStatus = user.Status is UserStatus.Blocked or UserStatus.Banned or UserStatus.Suspended;
            var failure = LoginHistory.RecordFailure(
                user.Id, request.IpAddress, request.UserAgent, "Account not active", context,
                isBlockedStatus ? SecurityEventSeverity.Medium : null);
            dbContext.LoginHistory.Add(failure);
            await dbContext.SaveChangesAsync(cancellationToken);

            if (isBlockedStatus)
            {
                await securityEventLogger.LogAsync(
                    SecurityEventType.BlockedLogin,
                    SecurityEventSeverity.Medium,
                    $"Login rejected for {user.Email}: account is {user.Status}.",
                    targetUserId: user.Id,
                    ipAddress: request.IpAddress,
                    userAgent: request.UserAgent,
                    country: request.CountryCode,
                    city: request.City,
                    cancellationToken: cancellationToken);
            }

            // Externally indistinguishable from a wrong password — see the EmailNotConfirmed branch above.
            return Result.Failure<AuthTokenResponse>(IdentityErrors.InvalidCredentials);
        }

        if (user.LockoutEnd.HasValue && user.LockoutEnd > DateTimeOffset.UtcNow)
        {
            var failure = LoginHistory.RecordFailure(user.Id, request.IpAddress, request.UserAgent, "Account locked", context);
            dbContext.LoginHistory.Add(failure);
            await dbContext.SaveChangesAsync(cancellationToken);
            // Externally indistinguishable from a wrong password — see the EmailNotConfirmed branch above.
            return Result.Failure<AuthTokenResponse>(IdentityErrors.InvalidCredentials);
        }

        var isPasswordValid = passwordHasher.VerifyPassword(user.PasswordHash, request.Password);
        if (!isPasswordValid)
        {
            var security = await platformSettings.GetSecurityAsync(cancellationToken);
            user.RecordAccessFailure(
                security.MaxFailedAccessAttempts,
                TimeSpan.FromMinutes(security.LockoutDurationMinutes));

            var risk = riskScorer.ScoreFailedPassword(user.AccessFailedCount, security.MaxFailedAccessAttempts);

            var failure = LoginHistory.RecordFailure(user.Id, request.IpAddress, request.UserAgent, "Invalid credentials", context, risk);
            dbContext.LoginHistory.Add(failure);
            await dbContext.SaveChangesAsync(cancellationToken);

            await securityEventLogger.LogAsync(
                SecurityEventType.FailedLogin,
                risk,
                $"Failed login attempt for {user.Email}: invalid credentials.",
                targetUserId: user.Id,
                ipAddress: request.IpAddress,
                userAgent: request.UserAgent,
                country: request.CountryCode,
                city: request.City,
                cancellationToken: cancellationToken);

            // Whether this failed attempt just triggered a lockout is recorded above (LoginHistory +
            // SecurityEventLogger) but never surfaced in the response — externally indistinguishable
            // from any other wrong password, see the EmailNotConfirmed branch above.
            return Result.Failure<AuthTokenResponse>(IdentityErrors.InvalidCredentials);
        }

        if (await adminAccessResolver.HasAdminPortalAccessAsync(user.Id, cancellationToken))
        {
            var adminFailure = LoginHistory.RecordFailure(
                user.Id,
                request.IpAddress,
                request.UserAgent,
                "Admin account must use admin portal login",
                context);
            dbContext.LoginHistory.Add(adminFailure);
            await dbContext.SaveChangesAsync(cancellationToken);
            return Result.Failure<AuthTokenResponse>(IdentityErrors.AdminMustUseAdminPortal);
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
            request.RememberMe,
            context,
            cancellationToken);
    }
}
