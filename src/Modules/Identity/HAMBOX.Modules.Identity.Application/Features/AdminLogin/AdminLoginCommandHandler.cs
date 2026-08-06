using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Identity.Application.Abstractions;
using HAMBOX.Modules.Identity.Application.Authorization;
using HAMBOX.Modules.Identity.Application.Contracts;
using HAMBOX.Modules.Identity.Application.Errors;
using HAMBOX.Modules.Identity.Application.Options;
using HAMBOX.Modules.Identity.Domain.Audit;
using HAMBOX.Modules.Identity.Domain.Enums;
using HAMBOX.Modules.Identity.Domain.Sessions;
using HAMBOX.Modules.Identity.Domain.Tokens;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HAMBOX.Modules.Identity.Application.Features.AdminLogin;

internal sealed class AdminLoginCommandHandler(
    IIdentityDbContext dbContext,
    IPasswordHasher passwordHasher,
    IAdminAccessResolver adminAccessResolver,
    IOtpCodeGenerator otpCodeGenerator,
    IEmailService emailService,
    IAuthTokenIssuer authTokenIssuer,
    IPlatformSettingsProvider platformSettings,
    ISecurityBlocklistService blocklistService,
    ISecurityEventLogger securityEventLogger,
    IClientInfoParser clientInfoParser,
    ITrustedDeviceService trustedDeviceService,
    ILoginRiskScorer riskScorer) : IRequestHandler<AdminLoginCommand, Result<AdminLoginChallengeResponse>>
{
    public async Task<Result<AdminLoginChallengeResponse>> Handle(
        AdminLoginCommand request,
        CancellationToken cancellationToken)
    {
        var (browserName, osName, deviceType) = clientInfoParser.ParseUserAgent(request.UserAgent);
        var fingerprint = DeviceFingerprint.Compute(request.UserAgent);
        var context = new LoginContext(request.CountryCode, request.City, browserName, osName, deviceType, fingerprint);

        var otp = await platformSettings.GetOtpAsync(cancellationToken);
        var security = await platformSettings.GetSecurityAsync(cancellationToken);
        var authentication = await platformSettings.GetAuthenticationAsync(cancellationToken);
        var normalizedEmail = request.Email.ToUpperInvariant();
        var user = await dbContext.Users
            .FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail, cancellationToken);

        if (user is null)
        {
            return Result.Failure<AdminLoginChallengeResponse>(IdentityErrors.InvalidCredentials);
        }

        if (await blocklistService.IsEmailBlockedAsync(user.Email, cancellationToken))
        {
            await RecordFailureAsync(user.Id, request, "Email address is blocked", context, SecurityEventSeverity.High, cancellationToken);
            await securityEventLogger.LogAsync(
                SecurityEventType.BlockedLogin,
                SecurityEventSeverity.High,
                $"Admin login rejected for {user.Email}: email address is blocked.",
                targetUserId: user.Id,
                ipAddress: request.IpAddress,
                userAgent: request.UserAgent,
                country: request.CountryCode,
                city: request.City,
                cancellationToken: cancellationToken);
            return Result.Failure<AdminLoginChallengeResponse>(IdentityErrors.InvalidCredentials);
        }

        if (await trustedDeviceService.IsDeviceBlockedAsync(user.Id, fingerprint, cancellationToken))
        {
            await RecordFailureAsync(user.Id, request, "Device is blocked", context, SecurityEventSeverity.High, cancellationToken);
            await securityEventLogger.LogAsync(
                SecurityEventType.DeviceBlock,
                SecurityEventSeverity.High,
                $"Admin login rejected for {user.Email}: device is blocked.",
                targetUserId: user.Id,
                ipAddress: request.IpAddress,
                userAgent: request.UserAgent,
                country: request.CountryCode,
                city: request.City,
                cancellationToken: cancellationToken);
            return Result.Failure<AdminLoginChallengeResponse>(IdentityErrors.InvalidCredentials);
        }

        if (!user.EmailConfirmed)
        {
            await RecordFailureAsync(user.Id, request, "Email not confirmed", context, null, cancellationToken);
            return Result.Failure<AdminLoginChallengeResponse>(IdentityErrors.EmailNotConfirmed);
        }

        if (user.Status != UserStatus.Active)
        {
            var isBlockedStatus = user.Status is UserStatus.Blocked or UserStatus.Banned or UserStatus.Suspended;
            await RecordFailureAsync(
                user.Id, request, "Account not active", context,
                isBlockedStatus ? SecurityEventSeverity.Medium : null, cancellationToken);

            if (isBlockedStatus)
            {
                await securityEventLogger.LogAsync(
                    SecurityEventType.BlockedLogin,
                    SecurityEventSeverity.Medium,
                    $"Admin login rejected for {user.Email}: account is {user.Status}.",
                    targetUserId: user.Id,
                    ipAddress: request.IpAddress,
                    userAgent: request.UserAgent,
                    country: request.CountryCode,
                    city: request.City,
                    cancellationToken: cancellationToken);
            }

            return Result.Failure<AdminLoginChallengeResponse>(IdentityErrors.AccountNotActive);
        }

        if (user.LockoutEnd.HasValue && user.LockoutEnd > DateTimeOffset.UtcNow)
        {
            await RecordFailureAsync(user.Id, request, "Account locked", context, null, cancellationToken);
            return Result.Failure<AdminLoginChallengeResponse>(IdentityErrors.AccountLocked);
        }

        if (!passwordHasher.VerifyPassword(user.PasswordHash, request.Password))
        {
            user.RecordAccessFailure(
                security.MaxFailedAccessAttempts,
                TimeSpan.FromMinutes(security.LockoutDurationMinutes));

            var risk = riskScorer.ScoreFailedPassword(user.AccessFailedCount, security.MaxFailedAccessAttempts);
            await RecordFailureAsync(user.Id, request, "Invalid credentials", context, risk, cancellationToken);

            if (user.LockoutEnd.HasValue && user.LockoutEnd > DateTimeOffset.UtcNow)
            {
                return Result.Failure<AdminLoginChallengeResponse>(IdentityErrors.AccountLocked);
            }

            return Result.Failure<AdminLoginChallengeResponse>(IdentityErrors.InvalidCredentials);
        }

        if (!await adminAccessResolver.HasAdminPortalAccessAsync(user.Id, cancellationToken))
        {
            await RecordFailureAsync(user.Id, request, "Admin portal access denied", context, null, cancellationToken);
            return Result.Failure<AdminLoginChallengeResponse>(IdentityErrors.AdminPortalAccessDenied);
        }

        user.ResetAccessFailedCount();

        var isNewCountry = !string.IsNullOrEmpty(request.CountryCode) && !await dbContext.LoginHistory.AnyAsync(
            h => h.UserId == user.Id && h.IsSuccessful && h.CountryCode == request.CountryCode, cancellationToken);
        var isNewDevice = await trustedDeviceService.RecordLoginAsync(user.Id, fingerprint, context, request.IpAddress, cancellationToken);
        var successRisk = riskScorer.ScoreSuccessfulLogin(isNewDevice, isNewCountry);

        if (!authentication.AdminOtpEnabled)
        {
            dbContext.AdminOtpAuditLogs.Add(AdminOtpAuditLog.Record(
                AdminOtpAuditLog.ActionBypassed,
                request.IpAddress,
                user.Id,
                details: "Admin OTP disabled via Platform Settings (Authentication.AdminOtpEnabled=false)"));
            dbContext.LoginHistory.Add(LoginHistory.RecordSuccess(user.Id, request.IpAddress, request.UserAgent, context, successRisk));
            await dbContext.SaveChangesAsync(cancellationToken);

            var tokenResult = await authTokenIssuer.IssueAsync(
                user,
                AuthContextTypes.Admin,
                otpVerified: true,
                request.IpAddress,
                request.UserAgent,
                rememberMe: false,
                context,
                cancellationToken);

            if (tokenResult.IsFailure)
            {
                return Result.Failure<AdminLoginChallengeResponse>(tokenResult.Error);
            }

            return Result.Success(new AdminLoginChallengeResponse(
                Guid.Empty,
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow,
                MaskEmail(user.Email),
                tokenResult.Value));
        }

        var activeChallenges = await dbContext.AdminLoginChallenges
            .Where(c => c.UserId == user.Id && c.UsedOnUtc == null && c.ExpiresOnUtc > DateTimeOffset.UtcNow)
            .ToListAsync(cancellationToken);

        foreach (var stale in activeChallenges)
        {
            stale.MarkUsed();
        }

        var code = otpCodeGenerator.GenerateNumericCode(otp.CodeLength);
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(otp.ExpirationMinutes);
        var challenge = AdminLoginChallenge.Create(
            user.Id,
            code,
            expiresAt,
            request.IpAddress,
            request.UserAgent);

        dbContext.AdminLoginChallenges.Add(challenge);
        dbContext.AdminOtpAuditLogs.Add(AdminOtpAuditLog.Record(
            AdminOtpAuditLog.ActionGenerated,
            request.IpAddress,
            user.Id,
            challenge.Id));
        dbContext.AdminOtpAuditLogs.Add(AdminOtpAuditLog.Record(
            AdminOtpAuditLog.ActionSent,
            request.IpAddress,
            user.Id,
            challenge.Id));

        await dbContext.SaveChangesAsync(cancellationToken);

        await emailService.SendAdminLoginOtpAsync(
            user.Id,
            user.Email,
            code,
            expiresAt,
            cancellationToken);

        var resendAvailableAt = DateTimeOffset.UtcNow.AddSeconds(otp.ResendCooldownSeconds);

        return Result.Success(new AdminLoginChallengeResponse(
            challenge.Id,
            expiresAt,
            resendAvailableAt,
            MaskEmail(user.Email)));
    }

    private async Task RecordFailureAsync(
        Guid userId,
        AdminLoginCommand request,
        string reason,
        LoginContext context,
        SecurityEventSeverity? riskLevel,
        CancellationToken cancellationToken)
    {
        dbContext.LoginHistory.Add(LoginHistory.RecordFailure(
            userId,
            request.IpAddress,
            request.UserAgent,
            reason,
            context,
            riskLevel));
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string MaskEmail(string email)
    {
        var at = email.IndexOf('@');
        if (at <= 1)
        {
            return "***";
        }

        return $"{email[0]}***{email[(at - 1)..]}";
    }
}
