using HAMBOX.Modules.Identity.Application.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace HAMBOX.Modules.Identity.Presentation.Extensions;

/// <summary>
/// Extension methods to configure authorization policies on route builders.
/// </summary>
public static class AuthorizationExtensions
{
    /// <summary>
    /// Requires the specified permission for the endpoint.
    /// </summary>
    /// <param name="builder">The route handler builder.</param>
    /// <param name="permission">The permission name.</param>
    /// <returns>The route handler builder.</returns>
    public static RouteHandlerBuilder RequirePermission(this RouteHandlerBuilder builder, string permission)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(permission);

        return builder.RequireAuthorization(permission);
    }

    /// <summary>
    /// Requires the specified permission for all endpoints in the group.
    /// </summary>
    /// <param name="builder">The route group builder.</param>
    /// <param name="permission">The permission name.</param>
    /// <returns>The route group builder.</returns>
    public static RouteGroupBuilder RequirePermission(this RouteGroupBuilder builder, string permission)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(permission);

        return builder.RequireAuthorization(permission);
    }

    /// <summary>
    /// Requires any one of the specified permissions (or Owner role).
    /// </summary>
    public static RouteHandlerBuilder RequireAnyPermission(
        this RouteHandlerBuilder builder,
        params string[] permissions)
    {
        ArgumentNullException.ThrowIfNull(permissions);

        if (permissions.Length == 1)
        {
            return builder.RequirePermission(permissions[0]);
        }

        if (permissions.Contains(PermissionConstants.Permissions.View) &&
            permissions.Contains(PermissionConstants.Roles.View) &&
            permissions.Length == 2)
        {
            return builder.RequireAuthorization(AuthorizationPolicies.PermissionMatrix);
        }

        throw new ArgumentException(
            "Composite permission policies must be registered in AuthorizationPolicies.",
            nameof(permissions));
    }

    /// <summary>
    /// Requires a storefront customer session (blocks admin portal tokens).
    /// </summary>
    public static RouteHandlerBuilder RequireCustomerContext(this RouteHandlerBuilder builder) =>
        builder.RequireAuthorization(AuthorizationPolicies.CustomerContext);

    /// <summary>
    /// Requires a storefront customer session for all endpoints in the group.
    /// </summary>
    public static RouteGroupBuilder RequireCustomerContext(this RouteGroupBuilder builder) =>
        builder.RequireAuthorization(AuthorizationPolicies.CustomerContext);
}
