namespace HAMBOX.Modules.Identity.Application.Authorization;

/// <summary>
/// Maps seeded roles to their catalog and admin permissions.
/// Used as a fallback when permission claims are missing from the access token.
/// </summary>
public static class RolePermissionMatrix
{
    private static readonly IReadOnlyDictionary<string, HashSet<string>> PermissionsByRole =
        new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase)
        {
            [RoleConstants.SuperAdmin] = PermissionConstants.All.ToHashSet(StringComparer.Ordinal),
            [RoleConstants.Admin] =
            [
                PermissionConstants.Products.Create,
                PermissionConstants.Products.Update,
                PermissionConstants.Products.Delete,
                PermissionConstants.Categories.Create,
                PermissionConstants.Categories.Update,
                PermissionConstants.Categories.Delete,
                PermissionConstants.Users.Read,
                PermissionConstants.Users.Update,
            ],
            [RoleConstants.ContentManager] =
            [
                PermissionConstants.Products.Create,
                PermissionConstants.Products.Update,
                PermissionConstants.Categories.Create,
                PermissionConstants.Categories.Update,
            ],
            [RoleConstants.SupportAgent] =
            [
                PermissionConstants.Users.Read,
            ],
        };

    public static bool RoleGrantsPermission(string roleName, string permission) =>
        PermissionsByRole.TryGetValue(roleName, out var permissions) && permissions.Contains(permission);
}
