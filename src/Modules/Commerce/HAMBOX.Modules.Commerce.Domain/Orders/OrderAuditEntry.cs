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
        string? actorDisplayName,
        string? reason,
        string? ipAddress,
        string? userAgent)
        : base(id)
    {
        OrderId = orderId;
        EventType = eventType;
        Description = description;
        ActorUserId = actorUserId;
        ActorDisplayName = actorDisplayName;
        Reason = reason;
        IpAddress = ipAddress;
        UserAgent = userAgent;
        OccurredOnUtc = DateTimeOffset.UtcNow;
    }

    public Guid OrderId { get; private set; }

    public string EventType { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    public string? ActorUserId { get; private set; }

    public string? ActorDisplayName { get; private set; }

    /// <summary>The justification the actor gave for a sensitive action (an admin revealing a license key).</summary>
    public string? Reason { get; private set; }

    /// <summary>Network address the action came from, as seen by the API (after trusted-proxy handling).</summary>
    public string? IpAddress { get; private set; }

    /// <summary>Browser / device User-Agent of the actor.</summary>
    public string? UserAgent { get; private set; }

    public DateTimeOffset OccurredOnUtc { get; private set; }

    public static OrderAuditEntry Create(
        Guid orderId,
        string eventType,
        string description,
        string? actorUserId = null,
        string? actorDisplayName = null,
        string? reason = null,
        string? ipAddress = null,
        string? userAgent = null) =>
        new(Guid.NewGuid(), orderId, eventType, description, actorUserId, actorDisplayName, reason, ipAddress, userAgent);
}
