using HAMBOX.Modules.Identity.Application.Abstractions;
using HAMBOX.Modules.Identity.Domain.Enums;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace HAMBOX.Modules.Identity.Infrastructure.Middleware;

/// <summary>
/// Rejects requests from a blocked IP address/range or blocked country before authentication runs,
/// so a ban applies to anonymous traffic (registration, login attempts) as well as authenticated
/// requests. Mirrors <see cref="MaintenanceModeMiddleware"/>'s shape: a runtime-configurable gate
/// resolved per-request via method injection, since the middleware itself is registered once as a
/// singleton pipeline step. Must run after <see cref="CountryResolutionMiddleware"/>, which supplies
/// the resolved country via <see cref="HttpContext.Items"/> — this middleware only ever consumes
/// that value and never resolves a country itself.
/// </summary>
public sealed class SecurityEnforcementMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, ISecurityBlocklistService blocklistService, ISecurityEventLogger securityEventLogger)
    {
        var path = context.Request.Path.Value ?? string.Empty;
        if (path.Contains("/health", StringComparison.OrdinalIgnoreCase)
            || path.Contains("/swagger", StringComparison.OrdinalIgnoreCase))
        {
            await next(context);
            return;
        }

        var ipAddress = context.Connection.RemoteIpAddress?.ToString();
        var correlationId = context.Items.TryGetValue("CorrelationId", out var value) ? value as string : null;

        if (!string.IsNullOrEmpty(ipAddress) && await blocklistService.IsIpBlockedAsync(ipAddress, context.RequestAborted))
        {
            await securityEventLogger.LogAsync(
                SecurityEventType.IpBlock,
                SecurityEventSeverity.High,
                $"Request from blocked IP {ipAddress} to {path} was rejected.",
                ipAddress: ipAddress,
                correlationId: correlationId,
                cancellationToken: context.RequestAborted);

            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new
            {
                code = "IP_BLOCKED",
                message = "Access from this network is not permitted.",
            });
            return;
        }

        var countryCode = context.Items.TryGetValue(CountryResolutionMiddleware.CountryItemKey, out var countryValue)
            ? countryValue as string
            : null;

        if (!string.IsNullOrEmpty(countryCode))
        {
            var countryStatus = await blocklistService.GetCountryStatusAsync(countryCode, context.RequestAborted);
            if (countryStatus is CountryRestrictionStatus.Blocked or CountryRestrictionStatus.TemporarilyBlocked)
            {
                await securityEventLogger.LogAsync(
                    SecurityEventType.CountryBlock,
                    SecurityEventSeverity.High,
                    $"Request from blocked country {countryCode} to {path} was rejected.",
                    ipAddress: ipAddress,
                    country: countryCode,
                    correlationId: correlationId,
                    cancellationToken: context.RequestAborted);

                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new
                {
                    code = "COUNTRY_BLOCKED",
                    message = "Access from this country is not permitted.",
                });
                return;
            }
        }

        await next(context);
    }
}

public static class SecurityEnforcementMiddlewareExtensions
{
    public static IApplicationBuilder UseSecurityEnforcement(this IApplicationBuilder app) =>
        app.UseMiddleware<SecurityEnforcementMiddleware>();
}
