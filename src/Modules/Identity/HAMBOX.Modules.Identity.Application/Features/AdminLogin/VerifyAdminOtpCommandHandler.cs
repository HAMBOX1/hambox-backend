using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Identity.Application.Abstractions;
using HAMBOX.Modules.Identity.Application.Authorization;
using HAMBOX.Modules.Identity.Application.Contracts;
using HAMBOX.Modules.Identity.Application.Errors;
using HAMBOX.Modules.Identity.Application.Options;
using HAMBOX.Modules.Identity.Domain.Audit;
using HAMBOX.Modules.Identity.Domain.Enums;
using HAMBOX.Modules.Identity.Domain.Sessions;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HAMBOX.Modules.Identity.Application.Features.AdminLogin;

internal sealed class VerifyAdminOtpCommandHandler(
    IIdentityDbContext dbContext,
    IAuthTokenIssuer authTokenIssuer,
    IPlatformSettingsProvider platformSettings) : IRequestHandler<VerifyAdminOtpCommand, Result<AuthTokenResponse>>
{
    public async Task<Result<AuthTokenResponse>> Handle(
        VerifyAdminOtpCommand request,
        CancellationToken cancellationToken)
    {
        var otp = await platformSettings.GetOtpAsync(cancellationToken);
        var challenge = await dbContext.AdminLoginChallenges
            .FirstOrDefaultAsync(c => c.Id == request.ChallengeId, cancellationToken);

        if (challenge is null)
        {
            return Result.Failure<AuthTokenResponse>(IdentityErrors.AdminLoginChallengeNotFound);
        }

        if (challenge.IsUsed)
        {
            return Result.Failure<AuthTokenResponse>(IdentityErrors.AdminLoginChallengeNotFound);
        }

        if (challenge.IsLocked)
        {
            return Result.Failure<AuthTokenResponse>(IdentityErrors.AdminOtpLocked);
        }

        if (challenge.IsExpired)
        {
            return Result.Failure<AuthTokenResponse>(IdentityErrors.AdminOtpExpired);
        }

        if (!challenge.VerifyCode(request.Code.Trim()))
        {
            challenge.RecordFailedAttempt(otp.MaxAttempts, otp.LockoutMinutes);
            dbContext.AdminOtpAuditLogs.Add(AdminOtpAuditLog.Record(
                AdminOtpAuditLog.ActionFailed,
                request.IpAddress,
                challenge.UserId,
                challenge.Id,
                $"Attempt {challenge.AttemptCount}"));

            if (challenge.IsLocked)
            {
                dbContext.AdminOtpAuditLogs.Add(AdminOtpAuditLog.Record(
                    AdminOtpAuditLog.ActionLocked,
                    request.IpAddress,
                    challenge.UserId,
                    challenge.Id));
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            return Result.Failure<AuthTokenResponse>(
                challenge.IsLocked ? IdentityErrors.AdminOtpLocked : IdentityErrors.AdminOtpInvalid);
        }

        var user = await dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == challenge.UserId, cancellationToken);

        if (user is null || user.Status != UserStatus.Active)
        {
            return Result.Failure<AuthTokenResponse>(IdentityErrors.AccountNotActive);
        }

        challenge.MarkUsed();
        dbContext.AdminOtpAuditLogs.Add(AdminOtpAuditLog.Record(
            AdminOtpAuditLog.ActionVerified,
            request.IpAddress,
            user.Id,
            challenge.Id));
        dbContext.LoginHistory.Add(LoginHistory.RecordSuccess(
            user.Id,
            request.IpAddress,
            request.UserAgent));

        await dbContext.SaveChangesAsync(cancellationToken);

        return await authTokenIssuer.IssueAsync(
            user,
            AuthContextTypes.Admin,
            otpVerified: true,
            request.IpAddress,
            request.UserAgent,
            rememberMe: false,
            cancellationToken: cancellationToken);
    }
}
