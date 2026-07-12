using HAMBOX.Domain.Entities;

namespace HAMBOX.Modules.Identity.Domain.Audit;

/// <summary>
/// Records security-relevant RBAC and authorization changes.
/// </summary>
public sealed class AuthorizationAuditLog : Entity
{
    private AuthorizationAuditLog()
    {
    }

    private AuthorizationAuditLog(
        Guid id,
        string action,
        string entityType,
        Guid? entityId,
        Guid actorUserId,
        string? details,
        string? ipAddress)
        : base(id)
    {
        Action = action;
        EntityType = entityType;
        EntityId = entityId;
        ActorUserId = actorUserId;
        Details = details;
        IpAddress = ipAddress;
    }

    public string Action { get; private set; } = string.Empty;

    public string EntityType { get; private set; } = string.Empty;

    public Guid? EntityId { get; private set; }

    public Guid ActorUserId { get; private set; }

    public string? Details { get; private set; }

    public string? IpAddress { get; private set; }

    public static AuthorizationAuditLog Create(
        string action,
        string entityType,
        Guid? entityId,
        Guid actorUserId,
        string? details = null,
        string? ipAddress = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(action);
        ArgumentException.ThrowIfNullOrWhiteSpace(entityType);

        if (actorUserId == Guid.Empty)
        {
            throw new ArgumentException("Actor user identifier is required.", nameof(actorUserId));
        }

        return new AuthorizationAuditLog(
            Guid.NewGuid(),
            action,
            entityType,
            entityId,
            actorUserId,
            details,
            ipAddress);
    }
}
