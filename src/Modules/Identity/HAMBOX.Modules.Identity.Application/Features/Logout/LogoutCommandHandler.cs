using HAMBOX.Modules.Identity.Application.Abstractions;
using HAMBOX.Modules.Identity.Domain.Enums;
using DomainRefreshToken = HAMBOX.Modules.Identity.Domain.Tokens.RefreshToken;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Identity.Application.Features.Logout;

/// <summary>
/// Handler for the <see cref="LogoutCommand"/> command.
/// </summary>
internal sealed class LogoutCommandHandler(
    IIdentityDbContext dbContext,
    ISecurityEventLogger securityEventLogger) : IRequestHandler<LogoutCommand, Result>
{
    /// <inheritdoc />
    public async Task<Result> Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        var tokenHash = DomainRefreshToken.GetLookupHash(request.RefreshToken);
        var token = await dbContext.RefreshTokens
            .FirstOrDefaultAsync(t => t.Token == tokenHash, cancellationToken);

        if (token is null)
        {
            return Result.Success();
        }

        if (!token.IsRevoked)
        {
            token.Revoke();

            var session = await dbContext.UserSessions
                .FirstOrDefaultAsync(s => s.Id == token.SessionId, cancellationToken);

            session?.End();

            await dbContext.SaveChangesAsync(cancellationToken);

            await securityEventLogger.LogAsync(
                SecurityEventType.Logout,
                SecurityEventSeverity.Low,
                "User signed out.",
                targetUserId: token.UserId,
                ipAddress: request.IpAddress,
                cancellationToken: cancellationToken);
        }

        return Result.Success();
    }
}
