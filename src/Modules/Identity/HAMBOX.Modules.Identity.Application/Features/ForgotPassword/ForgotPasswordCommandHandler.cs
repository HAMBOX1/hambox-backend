using HAMBOX.Modules.Identity.Application.Abstractions;
using HAMBOX.Modules.Identity.Domain.Audit;
using HAMBOX.Modules.Identity.Domain.Enums;
using HAMBOX.Modules.Identity.Domain.Tokens;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Identity.Application.Features.ForgotPassword;

/// <summary>
/// Handler for the <see cref="ForgotPasswordCommand"/> command.
/// </summary>
internal sealed class ForgotPasswordCommandHandler(
    IIdentityDbContext dbContext,
    ITokenGenerator tokenGenerator,
    IEmailService emailService) : IRequestHandler<ForgotPasswordCommand, Result>
{
    /// <inheritdoc />
    public async Task<Result> Handle(ForgotPasswordCommand request, CancellationToken cancellationToken)
    {
        var normalizedEmail = request.Email.ToUpperInvariant();
        var user = await dbContext.Users
            .FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail, cancellationToken);

        // Always return success to prevent email enumeration
        if (user is null)
        {
            return Result.Success();
        }

        var tokenValue = tokenGenerator.GenerateSecureToken();
        var resetToken = PasswordResetToken.Create(
            user.Id,
            tokenValue,
            DateTimeOffset.UtcNow.AddHours(1));

        dbContext.PasswordResetTokens.Add(resetToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        var delivered = await emailService.SendPasswordResetAsync(
            user.Id,
            user.Email,
            resetToken.ExpiresOnUtc,
            tokenValue,
            cancellationToken);

        dbContext.CustomerOtpAuditLogs.Add(CustomerOtpAuditLog.Record(
            CustomerOtpPurpose.PasswordReset,
            CustomerOtpEventStatus.Pending,
            DateTimeOffset.UtcNow,
            resetToken.ExpiresOnUtc,
            user.Id,
            resetToken.Id,
            ipAddress: request.IpAddress,
            userAgent: request.UserAgent,
            correlationId: request.CorrelationId,
            emailDeliveryStatus: delivered ? CustomerOtpEmailDeliveryStatus.Sent : CustomerOtpEmailDeliveryStatus.Failed,
            description: "Password reset token requested."));
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
