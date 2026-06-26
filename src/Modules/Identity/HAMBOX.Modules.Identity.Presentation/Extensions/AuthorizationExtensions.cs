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
}
