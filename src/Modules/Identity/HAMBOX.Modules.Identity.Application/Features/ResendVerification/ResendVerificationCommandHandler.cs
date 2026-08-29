using HAMBOX.Modules.Identity.Application.Abstractions;
using HAMBOX.Modules.Identity.Domain.Audit;
using HAMBOX.Modules.Identity.Domain.Enums;
using HAMBOX.Modules.Identity.Domain.Tokens;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Identity.Application.Features.ResendVerification;

/// <summary>
/// Handler for the <see cref="ResendVerificationCommand"/> command.
/// </summary>
internal sealed class ResendVerificationCommandHandler(
    IIdentityDbContext dbContext,
    ITokenGenerator tokenGenerator,
    IEmailService emailService) : IRequestHandler<ResendVerificationCommand, Result>
{
    /// <inheritdoc />
    public async Task<Result> Handle(ResendVerificationCommand request, CancellationToken cancellationToken)
    {
        var normalizedEmail = request.Email.ToUpperInvariant();
        var user = await dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail, cancellationToken);

        if (user is null || user.EmailConfirmed)
        {
            return Result.Success();
        }

        var unusedTokens = await dbContext.EmailVerificationTokens
            .Where(t => t.UserId == user.Id && t.UsedOnUtc == null)
            .ToListAsync(cancellationToken);

        if (unusedTokens.Count > 0)
        {
            dbContext.EmailVerificationTokens.RemoveRange(unusedTokens);

            // The old token rows are hard-deleted above — this audit row is the only surviving trace
            // that they ever existed, so record it before the delete is saved.
            foreach (var superseded in unusedTokens)
            {
                dbContext.CustomerOtpAuditLogs.Add(CustomerOtpAuditLog.Record(
                    CustomerOtpPurpose.EmailVerification,
                    CustomerOtpEventStatus.Invalidated,
                    superseded.CreatedOnUtc,
                    superseded.ExpiresOnUtc,
                    user.Id,
                    superseded.Id,
                    ipAddress: request.IpAddress,
                    userAgent: request.UserAgent,
                    correlationId: request.CorrelationId,
                    description: "Superseded by a resend."));
            }
        }

        var verificationTokenValue = tokenGenerator.GenerateSecureToken();
        var verificationToken = EmailVerificationToken.Create(
            user.Id,
            verificationTokenValue,
            DateTimeOffset.UtcNow.AddHours(24));

        dbContext.EmailVerificationTokens.Add(verificationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        var delivered = await emailService.SendEmailVerificationAsync(
            user.Id,
            user.Email,
            verificationToken.ExpiresOnUtc,
            verificationTokenValue,
            cancellationToken);

        dbContext.CustomerOtpAuditLogs.Add(CustomerOtpAuditLog.Record(
            CustomerOtpPurpose.EmailVerification,
            CustomerOtpEventStatus.Pending,
            DateTimeOffset.UtcNow,
            verificationToken.ExpiresOnUtc,
            user.Id,
            verificationToken.Id,
            ipAddress: request.IpAddress,
            userAgent: request.UserAgent,
            correlationId: request.CorrelationId,
            emailDeliveryStatus: delivered ? CustomerOtpEmailDeliveryStatus.Sent : CustomerOtpEmailDeliveryStatus.Failed,
            description: "Verification token resent."));
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
