using HAMBOX.Modules.Identity.Application.Abstractions;
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
        }

        var verificationTokenValue = tokenGenerator.GenerateSecureToken();
        var verificationToken = EmailVerificationToken.Create(
            user.Id,
            verificationTokenValue,
            DateTimeOffset.UtcNow.AddHours(24));

        dbContext.EmailVerificationTokens.Add(verificationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        await emailService.SendEmailVerificationAsync(
            user.Id,
            user.Email,
            verificationToken.ExpiresOnUtc,
            verificationTokenValue,
            cancellationToken);

        return Result.Success();
    }
}
