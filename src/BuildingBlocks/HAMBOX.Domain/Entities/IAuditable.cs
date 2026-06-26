namespace HAMBOX.Domain.Entities;

/// <summary>
/// Represents an entity that captures user audit metadata.
/// </summary>
public interface IAuditable
{
    /// <summary>
    /// Gets the identifier of the user that created the entity.
    /// </summary>
    string? CreatedBy { get; }

    /// <summary>
    /// Gets the identifier of the user that last modified the entity.
    /// </summary>
    string? ModifiedBy { get; }
}
