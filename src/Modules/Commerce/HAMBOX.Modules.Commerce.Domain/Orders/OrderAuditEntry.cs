using HAMBOX.Domain.Entities;

namespace HAMBOX.Modules.Commerce.Domain.Orders;

/// <summary>
/// Immutable audit entry for order lifecycle events.
/// </summary>
public sealed class OrderAuditEntry : Entity
{
    private OrderAuditEntry()
    {
    }

    private OrderAuditEntry(
        Guid id,
        Guid orderId,
        string eventType,
        string description,
        string? actorUserId,
        string? actorDisplayName)
        : base(id)
    {
        OrderId = orderId;
        EventType = eventType;
        Description = description;
        ActorUserId = actorUserId;
        ActorDisplayName = actorDisplayName;
        OccurredOnUtc = DateTimeOffset.UtcNow;
    }

    public Guid OrderId { get; private set; }

    public string EventType { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    public string? ActorUserId { get; private set; }

    public string? ActorDisplayName { get; private set; }

    public DateTimeOffset OccurredOnUtc { get; private set; }

    public static OrderAuditEntry Create(
        Guid orderId,
        string eventType,
        string description,
        string? actorUserId = null,
        string? actorDisplayName = null) =>
        new(Guid.NewGuid(), orderId, eventType, description, actorUserId, actorDisplayName);
}
