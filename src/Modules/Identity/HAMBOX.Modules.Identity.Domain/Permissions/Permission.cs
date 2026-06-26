using HAMBOX.Domain.Entities;

namespace HAMBOX.Modules.Identity.Domain.Permissions;

/// <summary>
/// Represents a permission that can be assigned to roles.
/// </summary>
public sealed class Permission : Entity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Permission"/> class.
    /// </summary>
    /// <remarks>Required by EF Core.</remarks>
    private Permission()
    {
    }

    private Permission(Guid id, string name, string? description)
        : base(id)
    {
        Name = name;
        NormalizedName = name.ToUpperInvariant();
        Description = description;
    }

    /// <summary>
    /// Gets the permission name.
    /// </summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the normalized permission name used for lookups and uniqueness checks.
    /// </summary>
    public string NormalizedName { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the permission description.
    /// </summary>
    public string? Description { get; private set; }

    /// <summary>
    /// Creates a new permission.
    /// </summary>
    /// <param name="name">The permission name.</param>
    /// <param name="description">An optional description of the permission.</param>
    /// <returns>A new <see cref="Permission"/> instance.</returns>
    /// <exception cref="ArgumentException">Thrown when the name is null or whitespace.</exception>
    public static Permission Create(string name, string? description = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return new Permission(Guid.NewGuid(), name, description);
    }
}
