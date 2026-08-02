using HAMBOX.Modules.Identity.Application.Abstractions;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Identity.Application.Features.Security.Sessions;

internal sealed class RevokeAllUserSessionsCommandHandler(
    IIdentityDbContext dbContext,
    IUserAuthorizationInvalidationService invalidationService) : IRequestHandler<RevokeAllUserSessionsCommand, Result>
{
    public async Task<Result> Handle(RevokeAllUserSessionsCommand request, CancellationToken cancellationToken)
    {
        var tokens = await dbContext.RefreshTokens
            .Where(t => t.UserId == request.UserId && t.RevokedOnUtc == null)
            .ToListAsync(cancellationToken);

        foreach (var token in tokens)
        {
            if (!token.IsRevoked)
            {
                token.Revoke();
            }
        }

        var sessions = await dbContext.UserSessions
            .Where(s => s.UserId == request.UserId && s.EndedOnUtc == null)
            .ToListAsync(cancellationToken);

        foreach (var session in sessions)
        {
            session.End();
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await invalidationService.InvalidateUserAsync(request.UserId, cancellationToken);

        return Result.Success();
    }
}
