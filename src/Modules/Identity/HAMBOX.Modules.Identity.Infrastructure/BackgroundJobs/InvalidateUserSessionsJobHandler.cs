using HAMBOX.Application.BackgroundJobs;
using HAMBOX.Modules.Identity.Application.Abstractions;
using HAMBOX.Modules.Identity.Domain.Security;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Identity.Infrastructure.BackgroundJobs;

/// <summary>
/// Revokes every active refresh token and ends every active session for a user, run asynchronously
/// after a block/suspend/ban action. The instant-effect part of blocking (rejecting the user's
/// current access token) already happens synchronously via the user's security-stamp rotation
/// plus <c>IUserAuthorizationInvalidationService</c> — this job only cleans up the now-orphaned
/// token/session rows, which is safe to do a little after the fact.
/// </summary>
internal sealed class InvalidateUserSessionsJobHandler(IBackgroundJobSerializer serializer, IIdentityDbContext dbContext)
    : BackgroundJobHandlerBase<Guid>(serializer)
{
    public override string JobType => SecurityJobTypes.InvalidateUserSessions;

    public override async Task HandleAsync(Guid userId, IBackgroundJobContext context, CancellationToken cancellationToken)
    {
        var tokens = await dbContext.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedOnUtc == null)
            .ToListAsync(cancellationToken);

        foreach (var token in tokens.Where(t => !t.IsRevoked))
        {
            token.Revoke();
        }

        var sessions = await dbContext.UserSessions
            .Where(s => s.UserId == userId && s.EndedOnUtc == null)
            .ToListAsync(cancellationToken);

        foreach (var session in sessions)
        {
            session.End();
        }

        if (tokens.Count > 0 || sessions.Count > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
