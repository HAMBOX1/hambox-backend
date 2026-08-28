using HAMBOX.Modules.Identity.Application.Abstractions;
using HAMBOX.Modules.Identity.Application.Errors;
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
    IPasswordHasher passwordHasher) : IRequestHandler<ResetPasswordCommand, Result>
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
            return Result.Failure(IdentityErrors.InvalidToken);
        }

        if (token.IsUsed)
        {
            return Result.Failure(IdentityErrors.InvalidToken);
        }

        if (token.IsExpired)
        {
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

        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
