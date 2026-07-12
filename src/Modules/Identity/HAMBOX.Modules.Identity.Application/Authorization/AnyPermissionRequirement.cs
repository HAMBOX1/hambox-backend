using Microsoft.AspNetCore.Authorization;

namespace HAMBOX.Modules.Identity.Application.Authorization;

/// <summary>
/// Succeeds when the user has at least one of the specified permissions (or Owner role).
/// </summary>
public sealed class AnyPermissionRequirement : IAuthorizationRequirement
{
    public AnyPermissionRequirement(params string[] permissions)
    {
        Permissions = permissions ?? throw new ArgumentNullException(nameof(permissions));
    }

    public IReadOnlyCollection<string> Permissions { get; }
}
