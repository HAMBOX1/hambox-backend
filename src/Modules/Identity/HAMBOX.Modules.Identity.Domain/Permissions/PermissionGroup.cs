using HAMBOX.Domain.Entities;

namespace HAMBOX.Modules.Identity.Domain.Permissions;

/// <summary>
/// Groups related permissions for UI matrix display and module organization.
/// </summary>
public sealed class PermissionGroup : Entity
{
    private PermissionGroup()
    {
    }

    private PermissionGroup(Guid id, string key, string name, string module, int sortOrder, string? description)
        : base(id)
    {
        Key = key;
        Name = name;
        Module = module;
        SortOrder = sortOrder;
        Description = description;
    }

    public string Key { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public string Module { get; private set; } = string.Empty;

    public int SortOrder { get; private set; }

    public string? Description { get; private set; }

    public static PermissionGroup Create(string key, string name, string module, int sortOrder, string? description = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(module);

        return new PermissionGroup(Guid.NewGuid(), key, name, module, sortOrder, description);
    }
}
