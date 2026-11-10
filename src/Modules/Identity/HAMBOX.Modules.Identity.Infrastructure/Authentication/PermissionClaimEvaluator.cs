using System.Security.Claims;
using HAMBOX.Modules.Identity.Application.Authorization;

namespace HAMBOX.Modules.Identity.Infrastructure.Authentication;

/// <summary>
/// The one definition of "this principal holds this permission": an OTP-verified admin-context token that is either an
/// Owner or carries the permission claim. Shared by the endpoint policy handler and by field-level checks.
/// </summary>
internal static class PermissionClaimEvaluator
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

    public static bool HasPermission(ClaimsPrincipal user, string permission)
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

        var permissions = user.Claims
            .Where(c => PermissionClaimTypes.Contains(c.Type))
            .Select(c => c.Value)
            .ToHashSet(StringComparer.Ordinal);

        return permissions.Contains(permission);
    }
}
