using HAMBOX.Application.Security;
using HAMBOX.Modules.Identity.Application.Abstractions;
using HAMBOX.Modules.Identity.Domain.Audit;
using HAMBOX.Modules.Identity.Domain.Enums;
using HAMBOX.Modules.Identity.Domain.Tokens;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Identity.Application.Services;

/// <summary>
/// E-mailed one-time codes for sensitive customer actions (see <see cref="ICustomerActionOtpService"/>).
/// Uses the same OTP policy as the rest of the platform (length, validity, attempts, resend cool-down,
/// lock-out from Platform Settings → OTP) and delivers through the existing e-mail path.
/// </summary>
public sealed class CustomerActionOtpService(
    IIdentityDbContext dbContext,
    IOtpCodeGenerator codeGenerator,
    IEmailService emailService,
    HAMBOX.Application.Abstractions.IPlatformSettingsProvider platformSettings) : ICustomerActionOtpService
{
    public async Task<CustomerActionOtpIssueResult> IssueAsync(
        string userId, string purpose, string scopeKey, CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(userId, out var userGuid))
        {
            return new CustomerActionOtpIssueResult(CustomerActionOtpIssueStatus.UserNotFound);
        }

        var user = await dbContext.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userGuid, cancellationToken);
        if (user is null)
        {
            return new CustomerActionOtpIssueResult(CustomerActionOtpIssueStatus.UserNotFound);
        }

        var policy = await platformSettings.GetOtpAsync(cancellationToken);
        var masked = Mask(user.Email);
        var now = DateTimeOffset.UtcNow;

        var open = await dbContext.CustomerActionChallenges
            .Where(c => c.UserId == userGuid && c.Purpose == purpose && c.ScopeKey == scopeKey && c.UsedOnUtc == null)
            .OrderByDescending(c => c.LastSentOnUtc)
            .ToListAsync(cancellationToken);

        var latest = open.FirstOrDefault();
        if (latest is { IsLocked: true })
        {
            return new CustomerActionOtpIssueResult(
                CustomerActionOtpIssueStatus.Locked, latest.ExpiresOnUtc, latest.LockedUntilUtc, masked);
        }

        if (latest is not null && !latest.IsExpired)
        {
            var next = latest.LastSentOnUtc.AddSeconds(policy.ResendCooldownSeconds);
            if (next > now)
            {
                return new CustomerActionOtpIssueResult(
                    CustomerActionOtpIssueStatus.CooldownActive, latest.ExpiresOnUtc, next, masked);
            }
        }

        foreach (var stale in open)
        {
            stale.Supersede();
        }

        var code = codeGenerator.GenerateNumericCode(policy.CodeLength);
        var expiresAt = now.AddMinutes(policy.ExpirationMinutes);
        var challenge = CustomerActionChallenge.Create(userGuid, purpose, scopeKey, code, expiresAt);
        dbContext.CustomerActionChallenges.Add(challenge);
        await dbContext.SaveChangesAsync(cancellationToken);

        var delivered = await SendAsync(user.Email, code, policy.ExpirationMinutes, cancellationToken);

        dbContext.CustomerOtpAuditLogs.Add(CustomerOtpAuditLog.Record(
            CustomerOtpPurpose.LicenseKeyReveal,
            delivered ? CustomerOtpEventStatus.Pending : CustomerOtpEventStatus.Failed,
            now,
            expiresAt,
            userGuid,
            challenge.Id,
            emailDeliveryStatus: delivered ? CustomerOtpEmailDeliveryStatus.Sent : CustomerOtpEmailDeliveryStatus.Failed,
            description: "License key reveal code requested."));

        if (!delivered)
        {
            challenge.Supersede();
            await dbContext.SaveChangesAsync(cancellationToken);
            return new CustomerActionOtpIssueResult(CustomerActionOtpIssueStatus.DeliveryFailed);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return new CustomerActionOtpIssueResult(
            CustomerActionOtpIssueStatus.Sent, expiresAt, now.AddSeconds(policy.ResendCooldownSeconds), masked);
    }

    public async Task<CustomerActionOtpVerifyStatus> VerifyAsync(
        string userId, string purpose, string scopeKey, string code, CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(userId, out var userGuid) || string.IsNullOrWhiteSpace(code))
        {
            return CustomerActionOtpVerifyStatus.NotRequested;
        }

        var challenge = await dbContext.CustomerActionChallenges
            .Where(c => c.UserId == userGuid && c.Purpose == purpose && c.ScopeKey == scopeKey && c.UsedOnUtc == null)
            .OrderByDescending(c => c.LastSentOnUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (challenge is null)
        {
            return CustomerActionOtpVerifyStatus.NotRequested;
        }

        if (challenge.IsLocked)
        {
            return CustomerActionOtpVerifyStatus.Locked;
        }

        if (challenge.IsExpired)
        {
            return CustomerActionOtpVerifyStatus.Expired;
        }

        var policy = await platformSettings.GetOtpAsync(cancellationToken);

        if (!challenge.Matches(code))
        {
            challenge.RecordFailedAttempt(policy.MaxAttempts, policy.LockoutMinutes);
            dbContext.CustomerOtpAuditLogs.Add(CustomerOtpAuditLog.Record(
                CustomerOtpPurpose.LicenseKeyReveal,
                CustomerOtpEventStatus.Failed,
                challenge.LastSentOnUtc,
                challenge.ExpiresOnUtc,
                userGuid,
                challenge.Id,
                description: $"Wrong reveal code (attempt {challenge.AttemptCount})."));
            await dbContext.SaveChangesAsync(cancellationToken);

            return challenge.IsLocked ? CustomerActionOtpVerifyStatus.Locked : CustomerActionOtpVerifyStatus.Invalid;
        }

        challenge.MarkUsed();
        dbContext.CustomerOtpAuditLogs.Add(CustomerOtpAuditLog.Record(
            CustomerOtpPurpose.LicenseKeyReveal,
            CustomerOtpEventStatus.Used,
            challenge.LastSentOnUtc,
            challenge.ExpiresOnUtc,
            userGuid,
            challenge.Id,
            usedOnUtc: DateTimeOffset.UtcNow,
            description: "License key reveal code verified."));
        await dbContext.SaveChangesAsync(cancellationToken);

        return CustomerActionOtpVerifyStatus.Verified;
    }

    private async Task<bool> SendAsync(string email, string code, int validMinutes, CancellationToken cancellationToken)
    {
        try
        {
            await emailService.SendTemplatedEmailAsync(
                email,
                "Your HAMBOX code to show a license key",
                $"<p>Use this code to show your license key:</p>"
                + $"<p style=\"font-size:24px;letter-spacing:6px;font-weight:bold\">{code}</p>"
                + $"<p>It is valid for {validMinutes} minutes and works once. "
                + "If you did not ask to show a license key, ignore this e-mail and consider changing your password.</p>",
                correlationId: null,
                cancellationToken: cancellationToken);
            return true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return false;
        }
    }

    private static string Mask(string email)
    {
        var at = email.IndexOf('@');
        return at <= 1 ? "***" : $"{email[0]}***{email[(at - 1)..]}";
    }
}
