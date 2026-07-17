using HAMBOX.Modules.Identity.Application.Abstractions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace HAMBOX.Modules.Identity.Infrastructure.Middleware;

/// <summary>
/// Resolves the request's country via <see cref="ICountryResolver"/> and stores it on
/// <see cref="HttpContext.Items"/> under <see cref="CountryItemKey"/>, so downstream code
/// (<see cref="SecurityEnforcementMiddleware"/>, endpoints) can consume the resolved country
/// without knowing how it was obtained. Must run before <see cref="SecurityEnforcementMiddleware"/>.
/// </summary>
public sealed class CountryResolutionMiddleware(RequestDelegate next)
{
    /// <summary>
    /// The <see cref="HttpContext.Items"/> key the resolved ISO-3166 alpha-2 country code (or
    /// null) is stored under.
    /// </summary>
    public const string CountryItemKey = "Security.ResolvedCountryCode";

    public async Task InvokeAsync(HttpContext context, ICountryResolver countryResolver)
    {
        var headers = context.Request.Headers.ToDictionary(
            h => h.Key,
            h => h.Value.ToString(),
            StringComparer.OrdinalIgnoreCase);
        var ipAddress = context.Connection.RemoteIpAddress?.ToString();

        var country = await countryResolver.ResolveCountryAsync(
            new CountryResolutionRequest(ipAddress, headers),
            context.RequestAborted);

        context.Items[CountryItemKey] = country;

        await next(context);
    }
}

public static class CountryResolutionMiddlewareExtensions
{
    public static IApplicationBuilder UseCountryResolution(this IApplicationBuilder app) =>
        app.UseMiddleware<CountryResolutionMiddleware>();
}
