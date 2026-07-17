using HAMBOX.Domain.Entities;

namespace HAMBOX.Modules.Communication.Domain.Communication;

public sealed class CommunicationAuditLog : Entity
{
    private CommunicationAuditLog()
    {
    }

    private CommunicationAuditLog(
        Guid id,
        CommunicationAuditAction action,
        string? actorUserId,
        string? entityType,
        string? entityId,
        string? details)
        : base(id)
    {
        Action = action;
        ActorUserId = actorUserId;
        EntityType = entityType;
        EntityId = entityId;
        Details = details;
    }

    public CommunicationAuditAction Action { get; private set; }
    public string? ActorUserId { get; private set; }
    public string? EntityType { get; private set; }
    public string? EntityId { get; private set; }
    public string? Details { get; private set; }

    public static CommunicationAuditLog Create(
        CommunicationAuditAction action,
        string? actorUserId,
        string? entityType = null,
        string? entityId = null,
        string? details = null) =>
        new(Guid.NewGuid(), action, actorUserId, entityType, entityId, details);
}
