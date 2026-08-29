using HAMBOX.Modules.Identity.Application.Abstractions;
using HAMBOX.Modules.Identity.Application.Errors;
using HAMBOX.Modules.Identity.Domain.Audit;
using HAMBOX.Modules.Identity.Domain.Enums;
using HAMBOX.Modules.Identity.Domain.Tokens;
using HAMBOX.Modules.Identity.Domain.Users;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Identity.Application.Features.VerifyEmail;

/// <summary>
/// Handler for the <see cref="VerifyEmailCommand"/> command.
/// </summary>
internal sealed class VerifyEmailCommandHandler(
    IIdentityDbContext dbContext,
    ISecurityEventLogger securityEventLogger) : IRequestHandler<VerifyEmailCommand, Result>
{
    /// <inheritdoc />
    public async Task<Result> Handle(VerifyEmailCommand request, CancellationToken cancellationToken)
    {
        // EmailVerificationTokens.Token stores only the SHA-256 hash — the incoming plaintext (from
        // the verification link) must be hashed the same way before the lookup can match it.
        var lookupHash = EmailVerificationToken.GetLookupHash(request.Token);
        var token = await dbContext.EmailVerificationTokens
            .FirstOrDefaultAsync(t => t.Token == lookupHash, cancellationToken);

        if (token is null)
        {
            await RecordFailedAttemptAsync(request, userId: null, tokenId: null, issuedOnUtc: null, expiresOnUtc: null, cancellationToken);
            return Result.Failure(IdentityErrors.InvalidToken);
        }

        if (token.IsUsed)
        {
            await RecordFailedAttemptAsync(request, token.UserId, token.Id, token.CreatedOnUtc, token.ExpiresOnUtc, cancellationToken);
            return Result.Failure(IdentityErrors.InvalidToken);
        }

        if (token.IsExpired)
        {
            await RecordExpiredAttemptAsync(request, token, cancellationToken);
            return Result.Failure(IdentityErrors.TokenExpired);
        }

        var user = await dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == token.UserId, cancellationToken);

        if (user is null)
        {
            return Result.Failure(IdentityErrors.UserNotFound);
        }

        var customerRole = await dbContext.Roles
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.IsDefault, cancellationToken);

        if (customerRole is null)
        {
            return Result.Failure(IdentityErrors.DefaultRoleNotFound);
        }

        token.MarkAsUsed();
        user.ConfirmEmail();
        user.Activate();

        var hasCustomerRole = await dbContext.UserRoles
            .AnyAsync(ur => ur.UserId == user.Id && ur.RoleId == customerRole.Id, cancellationToken);

        if (!hasCustomerRole)
        {
            dbContext.UserRoles.Add(UserRole.Create(user.Id, customerRole.Id));
        }

        dbContext.CustomerOtpAuditLogs.Add(CustomerOtpAuditLog.Record(
            CustomerOtpPurpose.EmailVerification,
            CustomerOtpEventStatus.Used,
            token.CreatedOnUtc,
            token.ExpiresOnUtc,
            user.Id,
            token.Id,
            usedOnUtc: token.UsedOnUtc,
            ipAddress: request.IpAddress,
            userAgent: request.UserAgent,
            correlationId: request.CorrelationId,
            description: "Email verified."));

        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    /// <summary>Records a failed verification attempt (wrong or already-used token) in both the
    /// dedicated OTP audit trail and, for repeated-probing visibility, the Security Center feed.</summary>
    private async Task RecordFailedAttemptAsync(
        VerifyEmailCommand request,
        Guid? userId,
        Guid? tokenId,
        DateTimeOffset? issuedOnUtc,
        DateTimeOffset? expiresOnUtc,
        CancellationToken cancellationToken)
    {
        dbContext.CustomerOtpAuditLogs.Add(CustomerOtpAuditLog.Record(
            CustomerOtpPurpose.EmailVerification,
            CustomerOtpEventStatus.Failed,
            issuedOnUtc,
            expiresOnUtc,
            userId,
            tokenId,
            ipAddress: request.IpAddress,
            userAgent: request.UserAgent,
            correlationId: request.CorrelationId,
            description: "Email verification attempt with an invalid or already-used token."));
        await dbContext.SaveChangesAsync(cancellationToken);

        await securityEventLogger.LogAsync(
            SecurityEventType.CustomerOtpEvent,
            SecurityEventSeverity.Low,
            "Email verification attempted with an invalid or already-used token.",
            targetUserId: userId,
            ipAddress: request.IpAddress,
            userAgent: request.UserAgent,
            correlationId: request.CorrelationId,
            cancellationToken: cancellationToken);
    }

    /// <summary>Records a verification attempt against an expired token.</summary>
    private async Task RecordExpiredAttemptAsync(
        VerifyEmailCommand request,
        EmailVerificationToken token,
        CancellationToken cancellationToken)
    {
        dbContext.CustomerOtpAuditLogs.Add(CustomerOtpAuditLog.Record(
            CustomerOtpPurpose.EmailVerification,
            CustomerOtpEventStatus.Expired,
            token.CreatedOnUtc,
            token.ExpiresOnUtc,
            token.UserId,
            token.Id,
            ipAddress: request.IpAddress,
            userAgent: request.UserAgent,
            correlationId: request.CorrelationId,
            description: "Email verification attempted after the token expired."));
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
