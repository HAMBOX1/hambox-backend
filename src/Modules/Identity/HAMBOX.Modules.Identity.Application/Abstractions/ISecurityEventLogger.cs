using HAMBOX.Modules.Identity.Domain.Enums;

namespace HAMBOX.Modules.Identity.Application.Abstractions;

/// <summary>
/// Records Security Center events (failed/blocked logins, admin block actions, permission
/// denials, ...) to the security event log.
/// </summary>
public interface ISecurityEventLogger
{
    Task LogAsync(
        SecurityEventType eventType,
        SecurityEventSeverity severity,
        string description,
        Guid? actorUserId = null,
        Guid? targetUserId = null,
        string? ipAddress = null,
        string? country = null,
        string? userAgent = null,
        string? correlationId = null,
        CancellationToken cancellationToken = default);
}
