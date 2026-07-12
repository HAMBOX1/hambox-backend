using HAMBOX.Application.Abstractions;
using HAMBOX.Application.PlatformSettings;
using HAMBOX.Modules.Identity.Application.Abstractions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace HAMBOX.Modules.Identity.Infrastructure.Middleware;

/// <summary>
/// Blocks storefront API traffic when maintenance mode is enabled, except for allowed admin roles.
/// </summary>
public sealed class MaintenanceModeMiddleware(RequestDelegate next)
{
  public async Task InvokeAsync(HttpContext context, IPlatformSettingsProvider settingsProvider)
  {
    var path = context.Request.Path.Value ?? string.Empty;
    if (path.StartsWith("/api/v", StringComparison.OrdinalIgnoreCase)
        && !path.Contains("/settings", StringComparison.OrdinalIgnoreCase)
        && !path.Contains("/auth", StringComparison.OrdinalIgnoreCase)
        && !path.Contains("/health", StringComparison.OrdinalIgnoreCase)
        && !path.Contains("/platform-settings", StringComparison.OrdinalIgnoreCase))
    {
      var maintenance = await settingsProvider.GetMaintenanceAsync(context.RequestAborted);
      if (maintenance.Enabled)
      {
        var role = context.User.FindFirst("role")?.Value
            ?? context.User.FindFirst("http://schemas.microsoft.com/ws/2008/06/identity/claims/role")?.Value;

        var allowed = maintenance.AllowedRoleNames.Any(r =>
            string.Equals(r, role, StringComparison.OrdinalIgnoreCase));

        if (!allowed)
        {
          context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
          await context.Response.WriteAsJsonAsync(new
          {
            code = "MAINTENANCE",
            message = maintenance.Message,
          });
          return;
        }
      }
    }

    await next(context);
  }
}

public static class MaintenanceModeMiddlewareExtensions
{
  public static IApplicationBuilder UseMaintenanceMode(this IApplicationBuilder app) =>
      app.UseMiddleware<MaintenanceModeMiddleware>();
}
