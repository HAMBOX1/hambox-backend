using Microsoft.AspNetCore.Authorization;

namespace HAMBOX.Modules.Identity.Application.Authorization;

/// <summary>
/// Represents an authorization requirement that checks for a specific permission.
/// </summary>
/// <param name="permission">The required permission name.</param>
public sealed class PermissionRequirement(string permission) : IAuthorizationRequirement
{
    /// <summary>
    /// Gets the name of the required permission.
    /// </summary>
    public string Permission { get; } = permission;
}
