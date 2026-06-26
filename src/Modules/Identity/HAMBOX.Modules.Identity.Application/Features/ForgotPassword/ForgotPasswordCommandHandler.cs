using HAMBOX.Modules.Identity.Application.Abstractions;
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

        await emailService.SendPasswordResetAsync(
            user.Id,
            user.Email,
            resetToken.ExpiresOnUtc,
            tokenValue,
            cancellationToken);

        return Result.Success();
    }
}
