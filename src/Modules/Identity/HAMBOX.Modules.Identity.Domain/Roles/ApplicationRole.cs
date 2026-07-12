using HAMBOX.Domain.Entities;

namespace HAMBOX.Modules.Identity.Domain.Roles;

/// <summary>
/// Represents a role that can be assigned to users to grant permissions.
/// </summary>
public sealed class ApplicationRole : AggregateRoot
{
    private readonly List<RolePermission> _rolePermissions = [];

    private ApplicationRole()
    {
    }

    private ApplicationRole(
        Guid id,
        string name,
        string? description,
        bool isDefault,
        bool isSystem,
        int priorityLevel)
        : base(id)
    {
        Name = name;
        NormalizedName = name.ToUpperInvariant();
        Description = description;
        IsDefault = isDefault;
        IsSystem = isSystem;
        PriorityLevel = priorityLevel;
    }

    public string Name { get; private set; } = string.Empty;

    public string NormalizedName { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public bool IsDefault { get; private set; }

    /// <summary>
    /// System roles (Owner, Administrator, Customer) cannot be deleted.
    /// </summary>
    public bool IsSystem { get; private set; }

    /// <summary>
    /// Lower value = higher privilege. Owner = 0, Administrator = 10, Customer = 1000.
    /// </summary>
    public int PriorityLevel { get; private set; }

    public IReadOnlyCollection<RolePermission> RolePermissions => _rolePermissions.AsReadOnly();

    public IReadOnlyCollection<Guid> PermissionIds =>
        _rolePermissions.Select(rp => rp.PermissionId).ToList().AsReadOnly();

    public static ApplicationRole Create(
        string name,
        string? description = null,
        bool isDefault = false,
        bool isSystem = false,
        int priorityLevel = 500)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return new ApplicationRole(Guid.NewGuid(), name, description, isDefault, isSystem, priorityLevel);
    }

    public void UpdateDetails(string name, string? description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
        NormalizedName = name.ToUpperInvariant();
        Description = description;
    }

    public void SetPriorityLevel(int priorityLevel)
    {
        PriorityLevel = priorityLevel;
    }

    public void AddPermission(Guid permissionId)
    {
        if (permissionId == Guid.Empty)
        {
            throw new ArgumentException("Permission identifier is required.", nameof(permissionId));
        }

        if (_rolePermissions.Any(rp => rp.PermissionId == permissionId))
        {
            throw new InvalidOperationException("Permission is already assigned to this role.");
        }

        _rolePermissions.Add(RolePermission.Create(Id, permissionId));
    }

    public void RemovePermission(Guid permissionId)
    {
        var existing = _rolePermissions.FirstOrDefault(rp => rp.PermissionId == permissionId);
        if (existing is null)
        {
            throw new InvalidOperationException("Permission is not assigned to this role.");
        }

        _rolePermissions.Remove(existing);
    }

    public void SetPermissions(IEnumerable<Guid> permissionIds)
    {
        _rolePermissions.Clear();
        foreach (var permissionId in permissionIds.Distinct())
        {
            AddPermission(permissionId);
        }
    }

    public void MarkAsDefault()
    {
        IsDefault = true;
    }

    public void UnmarkAsDefault()
    {
        IsDefault = false;
    }
}
