namespace HAMBOX.Modules.Identity.Application.Authorization;

/// <summary>
/// Role seed identifiers and default permission assignments.
/// </summary>
public static class RoleDefinitionRegistry
{
    public static class RoleIds
    {
        public static readonly Guid Owner = new("10000000-0000-0000-0000-000000000001");
        public static readonly Guid Administrator = new("10000000-0000-0000-0000-000000000002");
        public static readonly Guid Customer = new("10000000-0000-0000-0000-000000000005");
    }

  /// <summary>Administrator gets all permissions except Owner-only bypass semantics.</summary>
    public static readonly IReadOnlyCollection<Guid> AdministratorPermissionIds =
        PermissionDefinitionRegistry.AllPermissionIds;

    public static readonly IReadOnlyCollection<Guid> CustomerPermissionIds = [];
}
