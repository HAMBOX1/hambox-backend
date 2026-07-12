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
    IPlatformSettingsProvider platformSettings) : IRequestHandler<AdminLoginCommand, Result<AdminLoginChallengeResponse>>
{
    public async Task<Result<AdminLoginChallengeResponse>> Handle(
        AdminLoginCommand request,
        CancellationToken cancellationToken)
    {
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

        if (!user.EmailConfirmed)
        {
            await RecordFailureAsync(user.Id, request, "Email not confirmed", cancellationToken);
            return Result.Failure<AdminLoginChallengeResponse>(IdentityErrors.EmailNotConfirmed);
        }

        if (user.Status != UserStatus.Active)
        {
            await RecordFailureAsync(user.Id, request, "Account not active", cancellationToken);
            return Result.Failure<AdminLoginChallengeResponse>(IdentityErrors.AccountNotActive);
        }

        if (user.LockoutEnd.HasValue && user.LockoutEnd > DateTimeOffset.UtcNow)
        {
            await RecordFailureAsync(user.Id, request, "Account locked", cancellationToken);
            return Result.Failure<AdminLoginChallengeResponse>(IdentityErrors.AccountLocked);
        }

        if (!passwordHasher.VerifyPassword(user.PasswordHash, request.Password))
        {
            user.RecordAccessFailure(
                security.MaxFailedAccessAttempts,
                TimeSpan.FromMinutes(security.LockoutDurationMinutes));

            await RecordFailureAsync(user.Id, request, "Invalid credentials", cancellationToken);

            if (user.LockoutEnd.HasValue && user.LockoutEnd > DateTimeOffset.UtcNow)
            {
                return Result.Failure<AdminLoginChallengeResponse>(IdentityErrors.AccountLocked);
            }

            return Result.Failure<AdminLoginChallengeResponse>(IdentityErrors.InvalidCredentials);
        }

        if (!await adminAccessResolver.HasAdminPortalAccessAsync(user.Id, cancellationToken))
        {
            await RecordFailureAsync(user.Id, request, "Admin portal access denied", cancellationToken);
            return Result.Failure<AdminLoginChallengeResponse>(IdentityErrors.AdminPortalAccessDenied);
        }

        user.ResetAccessFailedCount();

        if (!authentication.AdminOtpEnabled)
        {
            dbContext.AdminOtpAuditLogs.Add(AdminOtpAuditLog.Record(
                AdminOtpAuditLog.ActionBypassed,
                request.IpAddress,
                user.Id,
                details: "Admin OTP disabled via Platform Settings (Authentication.AdminOtpEnabled=false)"));
            dbContext.LoginHistory.Add(LoginHistory.RecordSuccess(user.Id, request.IpAddress, request.UserAgent));
            await dbContext.SaveChangesAsync(cancellationToken);

            var tokenResult = await authTokenIssuer.IssueAsync(
                user,
                AuthContextTypes.Admin,
                otpVerified: true,
                request.IpAddress,
                request.UserAgent,
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
        CancellationToken cancellationToken)
    {
        dbContext.LoginHistory.Add(LoginHistory.RecordFailure(
            userId,
            request.IpAddress,
            request.UserAgent,
            reason));
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
