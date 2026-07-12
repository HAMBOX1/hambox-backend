namespace HAMBOX.Modules.Identity.Application.Authorization;

/// <summary>
/// Named authorization policies for composite permission checks.
/// </summary>
public static class AuthorizationPolicies
{
    /// <summary>Requires <see cref="PermissionConstants.Permissions.View"/> or <see cref="PermissionConstants.Roles.View"/>.</summary>
    public const string PermissionMatrix = "auth:any:permission-matrix";

    /// <summary>Requires a storefront customer session (not admin portal).</summary>
    public const string CustomerContext = "auth:customer-context";
}
