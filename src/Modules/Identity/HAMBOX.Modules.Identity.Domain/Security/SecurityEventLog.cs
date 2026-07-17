using HAMBOX.Domain.Entities;
using HAMBOX.Modules.Identity.Domain.Enums;

namespace HAMBOX.Modules.Identity.Domain.Security;

/// <summary>
/// An immutable record of a security-relevant occurrence (failed login, blocked login, an
/// admin block/unblock action, a permission denial, ...). Never updated or soft-deleted once
/// written — the audit-trail equivalent of <see cref="Sessions.LoginHistory"/> but covering the
/// full Security Center event surface rather than just login attempts.
/// </summary>
public sealed class SecurityEventLog : Entity
{
    private SecurityEventLog()
    {
    }

    private SecurityEventLog(
        Guid id,
        SecurityEventType eventType,
        SecurityEventSeverity severity,
        string description,
        Guid? actorUserId,
        Guid? targetUserId,
        string? ipAddress,
        string? country,
        string? userAgent,
        string? correlationId)
        : base(id)
    {
        EventType = eventType;
        Severity = severity;
        Description = description;
        ActorUserId = actorUserId;
        TargetUserId = targetUserId;
        IpAddress = ipAddress;
        Country = country;
        UserAgent = userAgent;
        CorrelationId = correlationId;
        OccurredOnUtc = DateTimeOffset.UtcNow;
    }

    public SecurityEventType EventType { get; private set; }

    public SecurityEventSeverity Severity { get; private set; }

    public string Description { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the identifier of the administrator (or system actor) who caused this event, if any.
    /// Null for events with no authenticated actor (e.g. an anonymous failed login).
    /// </summary>
    public Guid? ActorUserId { get; private set; }

    /// <summary>
    /// Gets the identifier of the user this event concerns, if any.
    /// </summary>
    public Guid? TargetUserId { get; private set; }

    public string? IpAddress { get; private set; }

    public string? Country { get; private set; }

    public string? UserAgent { get; private set; }

    public string? CorrelationId { get; private set; }

    public DateTimeOffset OccurredOnUtc { get; private set; }

    public static SecurityEventLog Record(
        SecurityEventType eventType,
        SecurityEventSeverity severity,
        string description,
        Guid? actorUserId = null,
        Guid? targetUserId = null,
        string? ipAddress = null,
        string? country = null,
        string? userAgent = null,
        string? correlationId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        return new SecurityEventLog(
            Guid.NewGuid(),
            eventType,
            severity,
            description,
            actorUserId,
            targetUserId,
            ipAddress,
            country,
            userAgent,
            correlationId);
    }
}
