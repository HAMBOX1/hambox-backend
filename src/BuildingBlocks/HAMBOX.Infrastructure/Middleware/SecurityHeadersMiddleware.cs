using Microsoft.AspNetCore.Http;

namespace HAMBOX.Infrastructure.Middleware;

/// <summary>
/// Adds the baseline set of security response headers to every response, including ones a
/// downstream middleware short-circuits (429 from the rate limiter, 403 from
/// SecurityEnforcementMiddleware, 503 from MaintenanceModeMiddleware, etc.) — registered via
/// <see cref="HttpResponse.OnStarting"/> so it fires no matter which middleware ends up writing the
/// response, mirroring <see cref="CorrelationIdMiddleware"/>'s same technique. HSTS is handled
/// separately via the framework's own <c>UseHsts()</c> in <c>Program.cs</c>, not here — no
/// Content-Security-Policy is set in this pass (see remediation notes: the storefront/admin bundle
/// hasn't been audited for CSP-safe inline script/style usage).
/// </summary>
public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            context.Response.Headers["X-Content-Type-Options"] = "nosniff";
            context.Response.Headers["X-Frame-Options"] = "DENY";
            context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";

            // Responses to authenticated API calls can carry staff-only data (cost prices, revealed license keys,
            // exports). Never let a browser or shared proxy keep a copy; endpoints that set their own policy win.
            if (context.Request.Path.StartsWithSegments("/api")
                && context.Request.Headers.ContainsKey("Authorization")
                && !context.Response.Headers.ContainsKey("Cache-Control"))
            {
                context.Response.Headers["Cache-Control"] = "no-store";
            }

            return Task.CompletedTask;
        });

        await next(context);
    }
}
