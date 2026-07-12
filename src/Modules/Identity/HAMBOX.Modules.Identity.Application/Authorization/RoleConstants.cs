namespace HAMBOX.Modules.Identity.Application.Authorization;

/// <summary>
/// Well-known system role names.
/// </summary>
public static class RoleConstants
{
    /// <summary>Full system access; bypasses permission checks. Cannot be deleted.</summary>
    public const string Owner = "Owner";

    /// <summary>General administration with configurable permissions.</summary>
    public const string Administrator = "Administrator";

    /// <summary>Default role for registered customers.</summary>
    public const string Customer = "Customer";

    /// <summary>Legacy alias — maps to Owner for backward compatibility.</summary>
    public const string SuperAdmin = "SuperAdmin";

    /// <summary>Legacy alias — maps to Administrator for backward compatibility.</summary>
    public const string Admin = "Admin";

    public static readonly IReadOnlyCollection<string> SystemRoles =
        [Owner, Administrator, Customer];

    public static readonly IReadOnlyCollection<string> OwnerAliases =
        [Owner, SuperAdmin];

    public static bool IsOwnerRole(string roleName) =>
        OwnerAliases.Contains(roleName, StringComparer.OrdinalIgnoreCase);
}
