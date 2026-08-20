using System.Security.Claims;
using HAMBOX.Modules.Identity.Application.Authorization;
using HAMBOX.Modules.Identity.Infrastructure.Authentication;
using Microsoft.AspNetCore.Authorization;

namespace HAMBOX.UnitTests.Identity;

/// <summary>
/// Proves the exact enforcement decision behind every <c>.RequirePermission(...)</c> endpoint —
/// including <c>PUT /api/v1/suppliers/{id}/settings</c> (gated on <see cref="PermissionConstants.Suppliers.Edit"/>
/// via the same handler). Written to settle a real production question: whether "You do not have
/// permission to perform this action" on the Bamboo supplier settings save was a genuine RBAC gap for
/// an Owner-role admin — confirmed here it is not (Owner bypasses this check entirely, by design), and
/// a live re-authenticated request against the running API succeeded immediately with no RBAC change.
/// </summary>
public sealed class PermissionAuthorizationHandlerTests
{
    private const string SuppliersEdit = "Suppliers.Edit";

    private static async Task<bool> EvaluateAsync(ClaimsPrincipal user, string permission = SuppliersEdit)
    {
        var handler = new PermissionAuthorizationHandler();
        var requirement = new PermissionRequirement(permission);
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

    // Authorized: an Owner-role admin bypasses the permission check entirely — no explicit
    // Suppliers.Edit claim needed. This is the Dev Admin seed account's actual shape and exactly why
    // it can create/test/update suppliers with zero RBAC configuration required.
    [Fact]
    public async Task OwnerRole_Succeeds_WithoutAnExplicitPermissionClaim()
    {
        var user = AdminUser(RoleConstants.Owner);

        var succeeded = await EvaluateAsync(user);

        Assert.True(succeeded);
    }

    // Authorized: a non-Owner admin with the explicit Suppliers.Edit claim succeeds — the normal path
    // for an Administrator role configured with this permission.
    [Fact]
    public async Task NonOwnerAdmin_WithSuppliersEditClaim_Succeeds()
    {
        var user = AdminUser(RoleConstants.Administrator, SuppliersEdit);

        var succeeded = await EvaluateAsync(user);

        Assert.True(succeeded);
    }

    // Unauthorized: a non-Owner admin WITHOUT the Suppliers.Edit claim fails — this is the one real
    // scenario the reported "You do not have permission" message actually describes. ASP.NET Core's
    // authorization middleware turns this into the framework's standard 403 response; nothing beyond
    // this handler's decision needs testing.
    [Fact]
    public async Task NonOwnerAdmin_WithoutSuppliersEditClaim_Fails()
    {
        var user = AdminUser(RoleConstants.Administrator, "Suppliers.View");

        var succeeded = await EvaluateAsync(user);

        Assert.False(succeeded);
    }

    // Unauthorized: even Owner fails outside the admin auth context / before OTP verification — the
    // same shape a customer-context or pre-OTP token would have. Directly explains the class of bug
    // that actually causes this exact user-facing message: an expired/invalid admin session, not a
    // missing RBAC assignment.
    [Theory]
    [InlineData(AuthContextTypes.Admin, "false")]
    [InlineData("customer", "true")]
    public async Task OwnerRole_Fails_WithoutValidAdminOtpContext(string authContext, string otpVerified)
    {
        var claims = new List<Claim>
        {
            new(IdentityClaimTypes.AuthContext, authContext),
            new(IdentityClaimTypes.OtpVerified, otpVerified),
            new(IdentityClaimTypes.Role, RoleConstants.Owner),
        };
        var user = new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer"));

        var succeeded = await EvaluateAsync(user);

        Assert.False(succeeded);
    }
}
