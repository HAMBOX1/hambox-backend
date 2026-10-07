using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Identity.Application.Abstractions;
using HAMBOX.Modules.Identity.Application.Errors;
using HAMBOX.Modules.Identity.Domain.Enums;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Identity.Application.Features.Sessions;

internal sealed class RevokeSessionCommandHandler(
    IIdentityDbContext dbContext,
    ICurrentUserService currentUser,
    ISecurityEventLogger securityEventLogger) : IRequestHandler<RevokeSessionCommand, Result>
{
    public async Task<Result> Handle(RevokeSessionCommand request, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(currentUser.UserId, out var userId))
        {
            return Result.Failure(IdentityErrors.AuthenticationRequired);
        }

        // Scoped to the caller's own sessions only — a user can never revoke someone else's session
        // through this self-service endpoint (that's the separate, permission-gated admin endpoint).
        var session = await dbContext.UserSessions
            .FirstOrDefaultAsync(s => s.Id == request.SessionId && s.UserId == userId, cancellationToken);

        if (session is null)
        {
            return Result.Failure(IdentityErrors.SessionNotFound);
        }

        if (session.IsActive)
        {
            session.End();
        }

        var token = await dbContext.RefreshTokens
            .FirstOrDefaultAsync(t => t.SessionId == session.Id && t.RevokedOnUtc == null, cancellationToken);

        if (token is not null && !token.IsRevoked)
        {
            token.Revoke();
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        await securityEventLogger.LogAsync(
            SecurityEventType.SessionRevoked,
            SecurityEventSeverity.Low,
            "User revoked one of their own sessions.",
            targetUserId: userId,
            cancellationToken: cancellationToken);

        return Result.Success();
    }
}
