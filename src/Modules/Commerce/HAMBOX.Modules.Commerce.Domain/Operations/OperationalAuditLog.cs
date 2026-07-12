namespace HAMBOX.Modules.Commerce.Domain.Operations;

public sealed class OperationalAuditLog
{
    private OperationalAuditLog()
    {
    }

    private OperationalAuditLog(
        Guid id,
        string action,
        string? entityType,
        string? entityId,
        string? actorUserId,
        string? actorDisplayName,
        string? details)
    {
        Id = id;
        Action = action;
        EntityType = entityType;
        EntityId = entityId;
        ActorUserId = actorUserId;
        ActorDisplayName = actorDisplayName;
        Details = details;
        OccurredOnUtc = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }
    public string Action { get; private set; } = string.Empty;
    public string? EntityType { get; private set; }
    public string? EntityId { get; private set; }
    public string? ActorUserId { get; private set; }
    public string? ActorDisplayName { get; private set; }
    public string? Details { get; private set; }
    public DateTimeOffset OccurredOnUtc { get; private set; }

    public static OperationalAuditLog Create(
        string action,
        string? actorUserId,
        string? actorDisplayName,
        string? entityType = null,
        string? entityId = null,
        string? details = null) =>
        new(Guid.NewGuid(), action, entityType, entityId, actorUserId, actorDisplayName, details);
}
