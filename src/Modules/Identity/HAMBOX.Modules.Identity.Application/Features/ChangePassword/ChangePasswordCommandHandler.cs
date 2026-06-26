using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Identity.Application.Abstractions;
using HAMBOX.Modules.Identity.Application.Errors;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Identity.Application.Features.ChangePassword;

/// <summary>
/// Handler for the <see cref="ChangePasswordCommand"/> command.
/// </summary>
internal sealed class ChangePasswordCommandHandler(
    IIdentityDbContext dbContext,
    ICurrentUserService currentUserService,
    IPasswordHasher passwordHasher) : IRequestHandler<ChangePasswordCommand, Result>
{
    /// <inheritdoc />
    public async Task<Result> Handle(ChangePasswordCommand request, CancellationToken cancellationToken)
    {
        if (!currentUserService.IsAuthenticated || currentUserService.UserId is null)
        {
            return Result.Failure(IdentityErrors.AuthenticationRequired);
        }

        if (!Guid.TryParse(currentUserService.UserId, out var userId))
        {
            return Result.Failure(IdentityErrors.AuthenticationRequired);
        }

        var user = await dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user is null)
        {
            return Result.Failure(IdentityErrors.UserNotFound);
        }

        var isCurrentPasswordValid = passwordHasher.VerifyPassword(user.PasswordHash, request.CurrentPassword);

        if (!isCurrentPasswordValid)
        {
            return Result.Failure(IdentityErrors.InvalidCurrentPassword);
        }

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
