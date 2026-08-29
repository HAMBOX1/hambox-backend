using HAMBOX.Modules.Identity.Application.Abstractions;
using HAMBOX.Modules.Identity.Application.Errors;
using HAMBOX.Modules.Identity.Domain.Audit;
using HAMBOX.Modules.Identity.Domain.Enums;
using HAMBOX.Modules.Identity.Domain.Tokens;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Identity.Application.Features.ResetPassword;

/// <summary>
/// Handler for the <see cref="ResetPasswordCommand"/> command.
/// </summary>
internal sealed class ResetPasswordCommandHandler(
    IIdentityDbContext dbContext,
    IPasswordHasher passwordHasher,
    ISecurityEventLogger securityEventLogger) : IRequestHandler<ResetPasswordCommand, Result>
{
    /// <inheritdoc />
    public async Task<Result> Handle(ResetPasswordCommand request, CancellationToken cancellationToken)
    {
        // PasswordResetTokens.Token stores only the SHA-256 hash — the incoming plaintext (from the
        // reset link) must be hashed the same way before the lookup can match it.
        var lookupHash = PasswordResetToken.GetLookupHash(request.Token);
        var token = await dbContext.PasswordResetTokens
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
            dbContext.CustomerOtpAuditLogs.Add(CustomerOtpAuditLog.Record(
                CustomerOtpPurpose.PasswordReset,
                CustomerOtpEventStatus.Expired,
                token.CreatedOnUtc,
                token.ExpiresOnUtc,
                token.UserId,
                token.Id,
                ipAddress: request.IpAddress,
                userAgent: request.UserAgent,
                correlationId: request.CorrelationId,
                description: "Password reset attempted after the token expired."));
            await dbContext.SaveChangesAsync(cancellationToken);
            return Result.Failure(IdentityErrors.TokenExpired);
        }

        var user = await dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == token.UserId, cancellationToken);

        if (user is null)
        {
            return Result.Failure(IdentityErrors.UserNotFound);
        }

        token.MarkAsUsed();

        var newPasswordHash = passwordHasher.HashPassword(request.NewPassword);
        user.UpdatePasswordHash(newPasswordHash);

        var activeTokens = await dbContext.RefreshTokens
            .Where(t => t.UserId == user.Id && t.RevokedOnUtc == null && t.ExpiresOnUtc > DateTimeOffset.UtcNow)
            .ToListAsync(cancellationToken);

        foreach (var activeToken in activeTokens)
        {
            activeToken.Revoke();
        }

        dbContext.CustomerOtpAuditLogs.Add(CustomerOtpAuditLog.Record(
            CustomerOtpPurpose.PasswordReset,
            CustomerOtpEventStatus.Used,
            token.CreatedOnUtc,
            token.ExpiresOnUtc,
            user.Id,
            token.Id,
            usedOnUtc: token.UsedOnUtc,
            ipAddress: request.IpAddress,
            userAgent: request.UserAgent,
            correlationId: request.CorrelationId,
            description: "Password reset completed."));

        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    /// <summary>Records a failed reset attempt (wrong or already-used token) in both the dedicated
    /// OTP audit trail and, for repeated-probing visibility, the Security Center feed.</summary>
    private async Task RecordFailedAttemptAsync(
        ResetPasswordCommand request,
        Guid? userId,
        Guid? tokenId,
        DateTimeOffset? issuedOnUtc,
        DateTimeOffset? expiresOnUtc,
        CancellationToken cancellationToken)
    {
        dbContext.CustomerOtpAuditLogs.Add(CustomerOtpAuditLog.Record(
            CustomerOtpPurpose.PasswordReset,
            CustomerOtpEventStatus.Failed,
            issuedOnUtc,
            expiresOnUtc,
            userId,
            tokenId,
            ipAddress: request.IpAddress,
            userAgent: request.UserAgent,
            correlationId: request.CorrelationId,
            description: "Password reset attempted with an invalid or already-used token."));
        await dbContext.SaveChangesAsync(cancellationToken);

        await securityEventLogger.LogAsync(
            SecurityEventType.CustomerOtpEvent,
            SecurityEventSeverity.Medium,
            "Password reset attempted with an invalid or already-used token.",
            targetUserId: userId,
            ipAddress: request.IpAddress,
            userAgent: request.UserAgent,
            correlationId: request.CorrelationId,
            cancellationToken: cancellationToken);
    }
}
