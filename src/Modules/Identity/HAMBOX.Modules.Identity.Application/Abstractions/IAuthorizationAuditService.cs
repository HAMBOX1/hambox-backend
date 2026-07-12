using HAMBOX.Modules.Identity.Domain.Audit;

namespace HAMBOX.Modules.Identity.Application.Abstractions;

/// <summary>
/// Records RBAC-related audit events.
/// </summary>
public interface IAuthorizationAuditService
{
    Task RecordAsync(
        string action,
        string entityType,
        Guid? entityId,
        Guid actorUserId,
        string? details = null,
        string? ipAddress = null,
        CancellationToken cancellationToken = default);
}
