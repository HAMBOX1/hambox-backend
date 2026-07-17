using HAMBOX.Modules.Identity.Application.Abstractions;
using HAMBOX.Modules.Identity.Domain.Enums;
using HAMBOX.Modules.Identity.Domain.Security;
using HAMBOX.Modules.Identity.Infrastructure.Persistence;

namespace HAMBOX.Modules.Identity.Infrastructure.Services;

internal sealed class SecurityEventLogger(IdentityDbContext dbContext) : ISecurityEventLogger
{
    public async Task LogAsync(
        SecurityEventType eventType,
        SecurityEventSeverity severity,
        string description,
        Guid? actorUserId = null,
        Guid? targetUserId = null,
        string? ipAddress = null,
        string? country = null,
        string? userAgent = null,
        string? correlationId = null,
        CancellationToken cancellationToken = default)
    {
        var entry = SecurityEventLog.Record(
            eventType, severity, description, actorUserId, targetUserId, ipAddress, country, userAgent, correlationId);

        dbContext.SecurityEventLogs.Add(entry);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
