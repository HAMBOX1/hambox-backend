using HAMBOX.Application.Abstractions;
using HAMBOX.Application.PlatformSettings;
using HAMBOX.Modules.Identity.Application.Abstractions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace HAMBOX.Modules.Identity.Infrastructure.Middleware;

/// <summary>
/// Blocks storefront API traffic when maintenance mode is enabled, except for allowed admin roles
/// or a request carrying a valid maintenance-bypass token (see <see cref="IMaintenanceBypassTokenIssuer"/>).
/// </summary>
public sealed class MaintenanceModeMiddleware(RequestDelegate next)
{
  private const string BypassHeaderName = "X-Maintenance-Bypass";

  public async Task InvokeAsync(
      HttpContext context,
      IPlatformSettingsProvider settingsProvider,
      IMaintenanceBypassTokenIssuer bypassTokenIssuer)
  {
    var path = context.Request.Path.Value ?? string.Empty;
    if (path.StartsWith("/api/v", StringComparison.OrdinalIgnoreCase)
        && !path.Contains("/settings", StringComparison.OrdinalIgnoreCase)
        && !path.Contains("/auth", StringComparison.OrdinalIgnoreCase)
        && !path.Contains("/health", StringComparison.OrdinalIgnoreCase)
        && !path.Contains("/platform-settings", StringComparison.OrdinalIgnoreCase))
    {
      var maintenance = await settingsProvider.GetMaintenanceAsync(context.RequestAborted);

      // Maintenance mode (an operator-triggered outage message) takes precedence over the
      // General settings "Store Status" business toggle when both are somehow active at once.
      string? gateCode = null;
      var gateMessage = string.Empty;

      if (maintenance.Enabled)
      {
        gateCode = "MAINTENANCE";
        gateMessage = maintenance.Message;
      }
      else
      {
        var general = await settingsProvider.GetGeneralAsync(context.RequestAborted);
        if (string.Equals(general.StoreStatus, "Closed", StringComparison.OrdinalIgnoreCase))
        {
          gateCode = "STORE_CLOSED";
        }
        else if (string.Equals(general.StoreStatus, "ComingSoon", StringComparison.OrdinalIgnoreCase))
        {
          gateCode = "COMING_SOON";
        }
      }

      if (gateCode is not null)
      {
        var role = context.User.FindFirst("role")?.Value
            ?? context.User.FindFirst("http://schemas.microsoft.com/ws/2008/06/identity/claims/role")?.Value;

        var allowed = maintenance.AllowedRoleNames.Any(r =>
            string.Equals(r, role, StringComparison.OrdinalIgnoreCase));

        if (!allowed && context.Request.Headers.TryGetValue(BypassHeaderName, out var bypassToken))
        {
          allowed = bypassTokenIssuer.TryValidate(bypassToken.ToString());
        }

        if (!allowed)
        {
          context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
          await context.Response.WriteAsJsonAsync(new
          {
            code = gateCode,
            message = gateMessage,
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
