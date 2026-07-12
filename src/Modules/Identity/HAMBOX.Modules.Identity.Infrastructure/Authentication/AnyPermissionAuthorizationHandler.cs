using System.Security.Claims;
using HAMBOX.Modules.Identity.Application.Authorization;
using Microsoft.AspNetCore.Authorization;

namespace HAMBOX.Modules.Identity.Infrastructure.Authentication;

internal sealed class AnyPermissionAuthorizationHandler
    : AuthorizationHandler<AnyPermissionRequirement>
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

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        AnyPermissionRequirement requirement)
    {
        if (HasAnyPermission(context.User, requirement.Permissions))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }

    private static bool HasAnyPermission(ClaimsPrincipal user, IReadOnlyCollection<string> required)
    {
        var authContext = user.FindFirst(IdentityClaimTypes.AuthContext)?.Value;
        var otpVerified = user.FindFirst(IdentityClaimTypes.OtpVerified)?.Value;

        if (authContext != AuthContextTypes.Admin || otpVerified != "true")
        {
            return false;
        }

        if (user.Claims.Any(c =>
                RoleClaimTypes.Contains(c.Type) &&
                RoleConstants.IsOwnerRole(c.Value)))
        {
            return true;
        }

        var granted = user.Claims
            .Where(c => PermissionClaimTypes.Contains(c.Type))
            .Select(c => c.Value)
            .ToHashSet(StringComparer.Ordinal);

        return required.Any(granted.Contains);
    }
}
