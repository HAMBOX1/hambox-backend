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
using Microsoft.Extensions.Hosting;
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
    ILoginRiskScorer riskScorer,
    IHostEnvironment environment,
    IOptions<AdminSecondFactorOptions> secondFactorOptions) : IRequestHandler<AdminLoginCommand, Result<AdminLoginChallengeResponse>>
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

        // The credentials are judged FIRST (same policy as customer login): until the caller has proven the
        // password every failure is the generic InvalidCredentials, so the admin form cannot be used to learn
        // which addresses exist or what state they are in. Only the password holder is told why they are stopped.
        var isLocked = user.LockoutEnd.HasValue && user.LockoutEnd > DateTimeOffset.UtcNow;
        if (!passwordHasher.VerifyPassword(user.PasswordHash, request.Password))
        {
            if (!isLocked)
            {
                user.RecordAccessFailure(
                    security.MaxFailedAccessAttempts,
                    TimeSpan.FromMinutes(security.LockoutDurationMinutes));
            }

            var risk = riskScorer.ScoreFailedPassword(user.AccessFailedCount, security.MaxFailedAccessAttempts);
            await RecordFailureAsync(
                user.Id, request, isLocked ? "Account locked" : "Invalid credentials", context, risk, cancellationToken);

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

        if (isLocked)
        {
            await RecordFailureAsync(user.Id, request, "Account locked", context, null, cancellationToken);
            return Result.Failure<AdminLoginChallengeResponse>(IdentityErrors.AccountLocked);
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

        // An enrolled authenticator app is the admin's second factor and is always required — it is the
        // factor that does not depend on an inbox, so it also stands in for the e-mailed code.
        var hasTotp = await dbContext.AdminTotpCredentials.AnyAsync(
            c => c.UserId == user.Id && c.IsConfirmed, cancellationToken);
        if (hasTotp)
        {
            var activeTotp = await dbContext.AdminLoginChallenges
                .Where(c => c.UserId == user.Id && c.UsedOnUtc == null && c.ExpiresOnUtc > DateTimeOffset.UtcNow)
                .ToListAsync(cancellationToken);
            foreach (var stale in activeTotp)
            {
                stale.MarkUsed();
            }

            var totpExpiresAt = DateTimeOffset.UtcNow.AddMinutes(otp.ExpirationMinutes);
            var totpChallenge = AdminLoginChallenge.CreateTotp(user.Id, totpExpiresAt, request.IpAddress, request.UserAgent);
            dbContext.AdminLoginChallenges.Add(totpChallenge);
            dbContext.AdminOtpAuditLogs.Add(AdminOtpAuditLog.Record(
                AdminOtpAuditLog.ActionGenerated, request.IpAddress, user.Id, totpChallenge.Id, "Authenticator challenge"));
            await dbContext.SaveChangesAsync(cancellationToken);

            return Result.Success(new AdminLoginChallengeResponse(
                totpChallenge.Id, totpExpiresAt, totpExpiresAt, MaskEmail(user.Email), Token: null, Method: "Totp"));
        }

        // Production never signs an admin in on a password alone: the floor below cannot be switched off from
        // Platform Settings (a compromised Settings.Edit account must not be able to). The only way past it is
        // the server-side AdminSecondFactor:AllowOtpBypassInProduction option — an explicit, operator-owned
        // exception for an install whose e-mail delivery is not working yet — and every bypass is audited.
        var floorApplies = environment.IsProduction() && !secondFactorOptions.Value.AllowOtpBypassInProduction;
        var otpRequired = authentication.AdminOtpEnabled || floorApplies;
        if (!otpRequired)
        {
            dbContext.AdminOtpAuditLogs.Add(AdminOtpAuditLog.Record(
                AdminOtpAuditLog.ActionBypassed,
                request.IpAddress,
                user.Id,
                details: environment.IsProduction()
                    ? "Admin OTP bypassed: AdminSecondFactor:AllowOtpBypassInProduction is enabled on the server."
                    : "Admin OTP disabled via Platform Settings (Authentication.AdminOtpEnabled=false)"));
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

        await dbContext.SaveChangesAsync(cancellationToken);

        var delivered = await emailService.SendAdminLoginOtpAsync(
            user.Id,
            user.Email,
            code,
            expiresAt,
            cancellationToken);

        if (!delivered)
        {
            // The code never reached the admin — invalidate it immediately rather than leaving an
            // undelivered-but-still-guessable challenge active for its full expiry window, and fail
            // closed: no token has been issued, so this never lets anyone in without OTP.
            challenge.MarkUsed();
            dbContext.AdminOtpAuditLogs.Add(AdminOtpAuditLog.Record(
                AdminOtpAuditLog.ActionDeliveryFailed,
                request.IpAddress,
                user.Id,
                challenge.Id));
            await dbContext.SaveChangesAsync(cancellationToken);

            return Result.Failure<AdminLoginChallengeResponse>(IdentityErrors.AdminOtpDeliveryFailed);
        }

        dbContext.AdminOtpAuditLogs.Add(AdminOtpAuditLog.Record(
            AdminOtpAuditLog.ActionSent,
            request.IpAddress,
            user.Id,
            challenge.Id));
        await dbContext.SaveChangesAsync(cancellationToken);

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
