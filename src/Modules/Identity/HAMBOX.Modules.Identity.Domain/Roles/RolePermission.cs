using HAMBOX.Domain.Entities;

namespace HAMBOX.Modules.Identity.Domain.Roles;

/// <summary>
/// Join entity linking a role to a permission.
/// </summary>
public sealed class RolePermission : Entity
{
    private RolePermission()
    {
    }

    private RolePermission(Guid id, Guid roleId, Guid permissionId)
        : base(id)
    {
        RoleId = roleId;
        PermissionId = permissionId;
    }

    public Guid RoleId { get; private set; }

    public Guid PermissionId { get; private set; }

    public static RolePermission Create(Guid roleId, Guid permissionId)
    {
        if (roleId == Guid.Empty)
        {
            throw new ArgumentException("Role identifier is required.", nameof(roleId));
        }

        if (permissionId == Guid.Empty)
        {
            throw new ArgumentException("Permission identifier is required.", nameof(permissionId));
        }

        return new RolePermission(Guid.NewGuid(), roleId, permissionId);
    }
}
