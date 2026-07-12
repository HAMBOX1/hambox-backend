using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Identity.Application.Abstractions;
using HAMBOX.Modules.Identity.Application.Contracts;
using HAMBOX.Modules.Identity.Application.Errors;
using HAMBOX.Modules.Identity.Application.Features.AdminLogin;
using HAMBOX.Modules.Identity.Application.Options;
using HAMBOX.Modules.Identity.Domain.Audit;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HAMBOX.Modules.Identity.Application.Features.AdminLogin;

internal sealed class ResendAdminOtpCommandHandler(
    IIdentityDbContext dbContext,
    IOtpCodeGenerator otpCodeGenerator,
    IEmailService emailService,
    IPlatformSettingsProvider platformSettings) : IRequestHandler<ResendAdminOtpCommand, Result<AdminLoginChallengeResponse>>
{
    public async Task<Result<AdminLoginChallengeResponse>> Handle(
        ResendAdminOtpCommand request,
        CancellationToken cancellationToken)
    {
        var otp = await platformSettings.GetOtpAsync(cancellationToken);
        var challenge = await dbContext.AdminLoginChallenges
            .FirstOrDefaultAsync(c => c.Id == request.ChallengeId, cancellationToken);

        if (challenge is null || !challenge.IsActive)
        {
            return Result.Failure<AdminLoginChallengeResponse>(IdentityErrors.AdminLoginChallengeNotFound);
        }

        if (challenge.LastResendOnUtc.HasValue)
        {
            var nextResend = challenge.LastResendOnUtc.Value.AddSeconds(otp.ResendCooldownSeconds);
            if (DateTimeOffset.UtcNow < nextResend)
            {
                return Result.Failure<AdminLoginChallengeResponse>(IdentityErrors.AdminOtpResendCooldown);
            }
        }

        var user = await dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == challenge.UserId, cancellationToken);

        if (user is null)
        {
            return Result.Failure<AdminLoginChallengeResponse>(IdentityErrors.UserNotFound);
        }

        var code = otpCodeGenerator.GenerateNumericCode(otp.CodeLength);
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(otp.ExpirationMinutes);
        challenge.RecordResend(code, expiresAt);

        dbContext.AdminOtpAuditLogs.Add(AdminOtpAuditLog.Record(
            AdminOtpAuditLog.ActionResent,
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
