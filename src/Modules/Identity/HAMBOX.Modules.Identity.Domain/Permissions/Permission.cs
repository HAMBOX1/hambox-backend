using HAMBOX.Domain.Entities;

namespace HAMBOX.Modules.Identity.Domain.Permissions;

/// <summary>
/// Represents a permission that can be assigned to roles.
/// </summary>
public sealed class Permission : Entity
{
    private Permission()
    {
    }

    private Permission(Guid id, Guid groupId, string name, string? description, int sortOrder)
        : base(id)
    {
        GroupId = groupId;
        Name = name;
        NormalizedName = name.ToUpperInvariant();
        Description = description;
        SortOrder = sortOrder;
    }

    public Guid GroupId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string NormalizedName { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public int SortOrder { get; private set; }

    public static Permission Create(Guid groupId, string name, string? description = null, int sortOrder = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (groupId == Guid.Empty)
        {
            throw new ArgumentException("Permission group identifier is required.", nameof(groupId));
        }

        return new Permission(Guid.NewGuid(), groupId, name, description, sortOrder);
    }
}
