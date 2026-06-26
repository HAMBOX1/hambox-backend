using System.Security.Claims;
using HAMBOX.Modules.Identity.Application.Authorization;
using Microsoft.AspNetCore.Authorization;

namespace HAMBOX.Modules.Identity.Infrastructure.Authentication;

/// <summary>
/// Authorization handler that evaluates <see cref="PermissionRequirement"/> against user claims.
/// </summary>
internal sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    private static readonly string[] RoleClaimTypes =
    [
        IdentityClaimTypes.Role,
        ClaimTypes.Role,
        "http://schemas.microsoft.com/ws/2008/06/identity/claims/role",
    ];

    private static readonly string[] PermissionClaimTypes =
    [
        IdentityClaimTypes.Permission,
        "permissions",
        "http://schemas.microsoft.com/ws/2008/06/identity/claims/permission",
    ];

    /// <inheritdoc />
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        if (HasPermission(context.User, requirement.Permission))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }

    private static bool HasPermission(ClaimsPrincipal user, string permission)
    {
        if (user.IsInRole(RoleConstants.SuperAdmin))
        {
            return true;
        }

        var permissions = user.Claims
            .Where(c => PermissionClaimTypes.Contains(c.Type))
            .Select(c => c.Value)
            .ToHashSet(StringComparer.Ordinal);

        if (permissions.Contains(permission))
        {
            return true;
        }

        var roles = user.Claims
            .Where(c => RoleClaimTypes.Contains(c.Type))
            .Select(c => c.Value);

        return roles.Any(role => RolePermissionMatrix.RoleGrantsPermission(role, permission));
    }
}
