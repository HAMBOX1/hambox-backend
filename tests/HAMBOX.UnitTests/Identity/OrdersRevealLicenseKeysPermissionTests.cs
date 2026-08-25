using System.Security.Claims;
using HAMBOX.Modules.Identity.Application.Authorization;
using HAMBOX.Modules.Identity.Infrastructure.Authentication;
using Microsoft.AspNetCore.Authorization;

namespace HAMBOX.UnitTests.Identity;

/// <summary>
/// Proves the enforcement decision behind <c>GET .../license-keys/{id}/reveal</c>
/// (<c>OrderManagementEndpoints.cs</c>), which now requires <see cref="PermissionConstants.Orders.RevealLicenseKeys"/>
/// instead of the broad <see cref="PermissionConstants.Orders.View"/> — a routine order-lookup
/// permission should no longer be sufficient to reveal a plaintext digital license code. Mirrors
/// <see cref="PermissionAuthorizationHandlerTests"/>'s exact pattern.
/// </summary>
public sealed class OrdersRevealLicenseKeysPermissionTests
{
    private const string RevealLicenseKeys = "Orders.RevealLicenseKeys";

    private static async Task<bool> EvaluateAsync(ClaimsPrincipal user)
    {
        var handler = new PermissionAuthorizationHandler();
        var requirement = new PermissionRequirement(RevealLicenseKeys);
        var context = new AuthorizationHandlerContext([requirement], user, resource: null);

        await handler.HandleAsync(context);

        return context.HasSucceeded;
    }

    private static ClaimsPrincipal AdminUser(string role, params string[] permissions)
    {
        var claims = new List<Claim>
        {
            new(IdentityClaimTypes.AuthContext, AuthContextTypes.Admin),
            new(IdentityClaimTypes.OtpVerified, "true"),
            new(IdentityClaimTypes.Role, role),
        };
        claims.AddRange(permissions.Select(p => new Claim(IdentityClaimTypes.Permission, p)));

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer"));
    }

    // Denied: an Administrator with ONLY the broad Orders.View permission — exactly the access
    // level that used to be enough to reveal a plaintext license key — must now fail.
    [Fact]
    public async Task NonOwnerAdmin_WithOnlyOrdersView_Fails()
    {
        var user = AdminUser(RoleConstants.Administrator, PermissionConstants.Orders.View);

        var succeeded = await EvaluateAsync(user);

        Assert.False(succeeded);
    }

    // Allowed: an Administrator explicitly granted Orders.RevealLicenseKeys succeeds — the normal
    // path once an operator deliberately assigns the dedicated permission.
    [Fact]
    public async Task NonOwnerAdmin_WithOrdersRevealLicenseKeysClaim_Succeeds()
    {
        var user = AdminUser(RoleConstants.Administrator, PermissionConstants.Orders.View, RevealLicenseKeys);

        var succeeded = await EvaluateAsync(user);

        Assert.True(succeeded);
    }

    // Allowed: Owner bypasses the check entirely, as with every other permission — consistent with
    // PermissionSynchronizer keeping Owner's RolePermission rows in lockstep with every registered
    // permission, including this new one.
    [Fact]
    public async Task OwnerRole_Succeeds_WithoutAnExplicitPermissionClaim()
    {
        var user = AdminUser(RoleConstants.Owner);

        var succeeded = await EvaluateAsync(user);

        Assert.True(succeeded);
    }
}
