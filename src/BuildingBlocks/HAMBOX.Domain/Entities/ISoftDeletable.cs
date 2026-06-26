namespace HAMBOX.Domain.Entities;

/// <summary>
/// Represents an entity that can be marked as deleted without being physically removed.
/// </summary>
public interface ISoftDeletable
{
    /// <summary>
    /// Gets a value indicating whether the entity is deleted.
    /// </summary>
    bool IsDeleted { get; }

    /// <summary>
    /// Gets the date and time, in UTC, when the entity was deleted.
    /// </summary>
    DateTimeOffset? DeletedOnUtc { get; }
}
