namespace HAMBOX.Domain.Events;

/// <summary>
/// Represents the base type for domain events.
/// </summary>
public abstract record BaseDomainEvent : IDomainEvent
{
    /// <summary>
    /// Gets the date and time, in UTC, when the event occurred.
    /// </summary>
    public DateTimeOffset DateOccurredUtc { get; init; } = DateTimeOffset.UtcNow;
}
