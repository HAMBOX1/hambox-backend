using HAMBOX.Modules.Identity.Application.Abstractions;
using HAMBOX.Modules.Identity.Application.Errors;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Identity.Application.Features.Security.Sessions;

internal sealed class RevokeUserSessionCommandHandler(IIdentityDbContext dbContext)
    : IRequestHandler<RevokeUserSessionCommand, Result>
{
    public async Task<Result> Handle(RevokeUserSessionCommand request, CancellationToken cancellationToken)
    {
        var session = await dbContext.UserSessions
            .FirstOrDefaultAsync(s => s.Id == request.SessionId && s.UserId == request.UserId, cancellationToken);

        if (session is null)
        {
            return Result.Failure(IdentityErrors.SessionNotFound);
        }

        if (session.IsActive)
        {
            session.End();
        }

        // Ending the session alone is enough to reject this session's access token on its next
        // request — ISessionValidator.IsSessionActiveAsync checks UserSessions.EndedOnUtc per
        // request. We deliberately don't rotate the user's global security stamp here (that
        // would sign the user out of every other session too — reserved for revoke-all).
        var token = await dbContext.RefreshTokens
            .FirstOrDefaultAsync(t => t.SessionId == session.Id && t.RevokedOnUtc == null, cancellationToken);

        if (token is not null && !token.IsRevoked)
        {
            token.Revoke();
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
