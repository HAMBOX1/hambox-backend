using HAMBOX.Domain.Entities;

namespace HAMBOX.Modules.Identity.Domain.Roles;

/// <summary>
/// Represents a role that can be assigned to users to grant permissions.
/// </summary>
public sealed class ApplicationRole : AggregateRoot
{
    private readonly List<Guid> _permissionIds = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="ApplicationRole"/> class.
    /// </summary>
    /// <remarks>Required by EF Core.</remarks>
    private ApplicationRole()
    {
    }

    private ApplicationRole(Guid id, string name, string? description)
        : base(id)
    {
        Name = name;
        NormalizedName = name.ToUpperInvariant();
        Description = description;
    }

    /// <summary>
    /// Gets the role name.
    /// </summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the normalized role name used for lookups and uniqueness checks.
    /// </summary>
    public string NormalizedName { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the role description.
    /// </summary>
    public string? Description { get; private set; }

    /// <summary>
    /// Gets a value indicating whether this role is assigned to new users by default.
    /// </summary>
    public bool IsDefault { get; private set; }

    /// <summary>
    /// Gets the identifiers of the permissions assigned to this role.
    /// </summary>
    public IReadOnlyCollection<Guid> PermissionIds => _permissionIds.AsReadOnly();

    /// <summary>
    /// Creates a new role.
    /// </summary>
    /// <param name="name">The role name.</param>
    /// <param name="description">An optional description of the role.</param>
    /// <returns>A new <see cref="ApplicationRole"/> instance.</returns>
    /// <exception cref="ArgumentException">Thrown when the name is null or whitespace.</exception>
    public static ApplicationRole Create(string name, string? description = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return new ApplicationRole(Guid.NewGuid(), name, description);
    }

    /// <summary>
    /// Assigns a permission to this role.
    /// </summary>
    /// <param name="permissionId">The identifier of the permission to assign.</param>
    /// <exception cref="ArgumentException">Thrown when the permission identifier is empty.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the permission is already assigned.</exception>
    public void AddPermission(Guid permissionId)
    {
        if (permissionId == Guid.Empty)
        {
            throw new ArgumentException("Permission identifier is required.", nameof(permissionId));
        }

        if (_permissionIds.Contains(permissionId))
        {
            throw new InvalidOperationException("Permission is already assigned to this role.");
        }

        _permissionIds.Add(permissionId);
    }

    /// <summary>
    /// Removes a permission from this role.
    /// </summary>
    /// <param name="permissionId">The identifier of the permission to remove.</param>
    /// <exception cref="InvalidOperationException">Thrown when the permission is not assigned.</exception>
    public void RemovePermission(Guid permissionId)
    {
        if (!_permissionIds.Remove(permissionId))
        {
            throw new InvalidOperationException("Permission is not assigned to this role.");
        }
    }

    /// <summary>
    /// Marks this role as the default role assigned to new users.
    /// </summary>
    public void MarkAsDefault()
    {
        IsDefault = true;
    }

    /// <summary>
    /// Removes the default designation from this role.
    /// </summary>
    public void UnmarkAsDefault()
    {
        IsDefault = false;
    }
}
