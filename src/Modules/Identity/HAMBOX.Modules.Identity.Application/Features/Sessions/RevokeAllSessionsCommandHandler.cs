using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Identity.Application.Abstractions;
using HAMBOX.Modules.Identity.Application.Errors;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Identity.Application.Features.Sessions;

internal sealed class RevokeAllSessionsCommandHandler(
    IIdentityDbContext dbContext,
    ICurrentUserService currentUser,
    IUserAuthorizationInvalidationService invalidationService) : IRequestHandler<RevokeAllSessionsCommand, Result>
{
    public async Task<Result> Handle(RevokeAllSessionsCommand request, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(currentUser.UserId, out var userId))
        {
            return Result.Failure(IdentityErrors.AuthenticationRequired);
        }

        var tokens = await dbContext.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedOnUtc == null)
            .ToListAsync(cancellationToken);

        foreach (var token in tokens)
        {
            if (!token.IsRevoked)
            {
                token.Revoke();
            }
        }

        var sessions = await dbContext.UserSessions
            .Where(s => s.UserId == userId && s.EndedOnUtc == null)
            .ToListAsync(cancellationToken);

        foreach (var session in sessions)
        {
            session.End();
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await invalidationService.InvalidateUserAsync(userId, cancellationToken);

        return Result.Success();
    }
}
