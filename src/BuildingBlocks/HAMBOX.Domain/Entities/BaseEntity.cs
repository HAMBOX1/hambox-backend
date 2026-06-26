namespace HAMBOX.Domain.Entities;

/// <summary>
/// Represents the base type for all domain entities.
/// </summary>
public abstract class BaseEntity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BaseEntity"/> class.
    /// </summary>
    protected BaseEntity()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BaseEntity"/> class.
    /// </summary>
    /// <param name="id">The unique entity identifier.</param>
    protected BaseEntity(Guid id)
    {
        Id = id;
    }

    /// <summary>
    /// Gets the unique entity identifier.
    /// </summary>
    public Guid Id { get; protected set; }

    /// <summary>
    /// Gets the date and time, in UTC, when the entity was created.
    /// </summary>
    public DateTimeOffset CreatedOnUtc { get; protected set; }

    /// <summary>
    /// Gets the date and time, in UTC, when the entity was last modified.
    /// </summary>
    public DateTimeOffset? ModifiedOnUtc { get; protected set; }
}
