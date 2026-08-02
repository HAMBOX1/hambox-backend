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
        string? correlationId,
        string? city)
        : base(id)
    {
        EventType = eventType;
        Severity = severity;
        Description = description;
        ActorUserId = actorUserId;
        TargetUserId = targetUserId;
        IpAddress = ipAddress;
        Country = country;
        City = city;
        UserAgent = userAgent;
        CorrelationId = correlationId;
        OccurredOnUtc = DateTimeOffset.UtcNow;
        Status = SecurityEventStatus.Open;
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

    /// <summary>
    /// Gets the resolved city, when the configured <see cref="Application.Abstractions.ICountryResolver"/>
    /// supports city-level resolution (e.g. MaxMind GeoLite2-City).
    /// </summary>
    public string? City { get; private set; }

    public string? UserAgent { get; private set; }

    public string? CorrelationId { get; private set; }

    public DateTimeOffset OccurredOnUtc { get; private set; }

    /// <summary>
    /// Gets the investigation workflow state of this event.
    /// </summary>
    public SecurityEventStatus Status { get; private set; }

    public Guid? AcknowledgedByUserId { get; private set; }

    public DateTimeOffset? AcknowledgedOnUtc { get; private set; }

    public Guid? ResolvedByUserId { get; private set; }

    public DateTimeOffset? ResolvedOnUtc { get; private set; }

    public string? ResolutionNotes { get; private set; }

    public static SecurityEventLog Record(
        SecurityEventType eventType,
        SecurityEventSeverity severity,
        string description,
        Guid? actorUserId = null,
        Guid? targetUserId = null,
        string? ipAddress = null,
        string? country = null,
        string? userAgent = null,
        string? correlationId = null,
        string? city = null)
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
            correlationId,
            city);
    }

    /// <summary>
    /// Marks this event as acknowledged by an admin — "I've seen this," without necessarily
    /// having resolved the underlying concern.
    /// </summary>
    public void Acknowledge(Guid acknowledgedByUserId)
    {
        Status = SecurityEventStatus.Acknowledged;
        AcknowledgedByUserId = acknowledgedByUserId;
        AcknowledgedOnUtc = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Marks this event as dismissed — reviewed and judged not actionable (e.g. a known false
    /// positive), distinct from <see cref="Resolve"/> which implies a genuine issue was handled.
    /// </summary>
    public void Dismiss(Guid dismissedByUserId, string? notes = null)
    {
        Status = SecurityEventStatus.Dismissed;
        AcknowledgedByUserId = dismissedByUserId;
        AcknowledgedOnUtc = DateTimeOffset.UtcNow;
        ResolutionNotes = notes;
    }

    /// <summary>
    /// Marks this event as resolved — the underlying concern was investigated and handled.
    /// </summary>
    public void Resolve(Guid resolvedByUserId, string? notes = null)
    {
        Status = SecurityEventStatus.Resolved;
        ResolvedByUserId = resolvedByUserId;
        ResolvedOnUtc = DateTimeOffset.UtcNow;
        ResolutionNotes = notes;
    }
}
