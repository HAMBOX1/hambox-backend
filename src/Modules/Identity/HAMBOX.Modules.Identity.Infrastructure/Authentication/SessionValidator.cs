using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Identity.Application.Abstractions;
using HAMBOX.Modules.Identity.Application.Authorization;
using HAMBOX.Modules.Identity.Domain.Enums;
using HAMBOX.Modules.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Identity.Infrastructure.Authentication;

internal sealed class SessionValidator(
    IdentityDbContext dbContext,
    IPlatformSettingsProvider platformSettings,
    ISecurityEventLogger securityEventLogger) : ISessionValidator
{
    public async Task<bool> IsSessionActiveAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        if (sessionId == Guid.Empty)
        {
            return false;
        }

        var session = await dbContext.UserSessions
            .FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken);

        if (session is null || !session.IsActive)
        {
            return false;
        }

        // Admin-portal-only: reject (and end) a still-unexpired access token in real time, the
        // instant idle/absolute expiry is crossed, rather than waiting for the next refresh attempt —
        // same backend-authoritative enforcement as RefreshTokenCommandHandler, just on the read path.
        if (session.AuthContext != AuthContextTypes.Admin)
        {
            return true;
        }

        var auth = await platformSettings.GetAuthenticationAsync(cancellationToken);
        var idleExceeded = session.HasExceededIdleTimeout(TimeSpan.FromMinutes(auth.AdminIdleTimeoutMinutes));
        var lifetimeExceeded = session.HasExceededMaxLifetime(TimeSpan.FromHours(auth.AdminMaxSessionLifetimeHours));
        if (!idleExceeded && !lifetimeExceeded)
        {
            return true;
        }

        session.End();
        await dbContext.SaveChangesAsync(cancellationToken);

        await securityEventLogger.LogAsync(
            SecurityEventType.SessionExpired,
            SecurityEventSeverity.Low,
            idleExceeded
                ? $"Admin session ended: idle for longer than {auth.AdminIdleTimeoutMinutes} minutes."
                : $"Admin session ended: exceeded the {auth.AdminMaxSessionLifetimeHours}-hour maximum session lifetime.",
            targetUserId: session.UserId,
            cancellationToken: cancellationToken);

        return false;
    }
}
