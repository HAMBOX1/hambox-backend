using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Identity.Application.Abstractions;
using HAMBOX.Modules.Identity.Application.Errors;
using HAMBOX.Modules.Identity.Domain.Enums;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Identity.Application.Features.Sessions;

internal sealed class RevokeAllSessionsCommandHandler(
    IIdentityDbContext dbContext,
    ICurrentUserService currentUser,
    IUserAuthorizationInvalidationService invalidationService,
    ISecurityEventLogger securityEventLogger) : IRequestHandler<RevokeAllSessionsCommand, Result>
{
    public async Task<Result> Handle(RevokeAllSessionsCommand request, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(currentUser.UserId, out var userId))
        {
            return Result.Failure(IdentityErrors.AuthenticationRequired);
        }

        // "Sign out all other sessions" must preserve the caller's own session — otherwise the request
        // that asked for this logs the admin out too. currentUser.SessionId is null only for a token
        // issued before the session_id claim existed; falling back to "revoke everything" then matches
        // the prior (pre-fix) behavior rather than silently doing nothing.
        var currentSessionId = currentUser.SessionId;

        var tokens = await dbContext.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedOnUtc == null && t.SessionId != currentSessionId)
            .ToListAsync(cancellationToken);

        foreach (var token in tokens)
        {
            if (!token.IsRevoked)
            {
                token.Revoke();
            }
        }

        var sessions = await dbContext.UserSessions
            .Where(s => s.UserId == userId && s.EndedOnUtc == null && s.Id != currentSessionId)
            .ToListAsync(cancellationToken);

        foreach (var session in sessions)
        {
            session.End();
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        // InvalidateUserAsync rotates the user's security stamp, which immediately invalidates every
        // still-live access token for this user — including the caller's own. Only do that in the
        // legacy "no known current session" fallback (where the current session is being revoked too,
        // same as the pre-fix behavior); otherwise it would undo the "preserve current session"
        // exclusion above as soon as the caller's token is next validated.
        if (currentSessionId is null)
        {
            await invalidationService.InvalidateUserAsync(userId, cancellationToken);
        }

        await securityEventLogger.LogAsync(
            SecurityEventType.SignOutAllDevices,
            SecurityEventSeverity.Low,
            "User signed out of all other sessions.",
            targetUserId: userId,
            cancellationToken: cancellationToken);

        return Result.Success();
    }
}
