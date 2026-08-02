namespace HAMBOX.Modules.Identity.Application.Abstractions;

/// <summary>
/// Resolves the ISO-3166 alpha-2 country a request originates from, so Security Center country
/// blocking (<see cref="HAMBOX.Modules.Identity.Domain.Security.CountryRestriction"/>) can enforce
/// access without coupling to *how* that signal is obtained. Swap the DI registration to plug in a
/// real source later — an edge/CDN header (Cloudflare <c>CF-IPCountry</c>, AWS CloudFront
/// <c>CloudFront-Viewer-Country</c>, Azure Front Door's country header) or a GeoIP database
/// (MaxMind GeoLite2) — with no change to any Security Center business logic. The default
/// registration, <c>NullCountryResolver</c>, always returns null, which makes country enforcement
/// a no-op until a real resolver is registered.
/// </summary>
public interface ICountryResolver
{
    /// <summary>
    /// Resolves the ISO-3166 alpha-2 country (and, where the source supports it, the city) the
    /// given request signals originate from, or null if it cannot be determined.
    /// </summary>
    Task<GeoLocationResult?> ResolveCountryAsync(CountryResolutionRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// The raw per-request signals available to a country resolver: the client IP address and the
/// full request header collection, so a header-based resolver can read whichever header its
/// provider uses without requiring an interface change.
/// </summary>
public sealed record CountryResolutionRequest(string? IpAddress, IReadOnlyDictionary<string, string> Headers);

/// <summary>
/// The geolocation signals resolved for a request. <see cref="City"/> is only populated by
/// resolvers backed by a city-level database (e.g. MaxMind GeoLite2-City) — header-based
/// resolvers typically only ever populate <see cref="CountryCode"/>.
/// </summary>
public sealed record GeoLocationResult(string? CountryCode, string? CountryName, string? City);

/// <summary>
/// The <see cref="Microsoft.AspNetCore.Http.HttpContext"/> item keys the Infrastructure-layer
/// country-resolution middleware stashes its result under, so Presentation-layer endpoints can
/// read the already-resolved <see cref="GeoLocationResult"/> without a Presentation → Infrastructure
/// project reference (or a second, duplicate resolution).
/// </summary>
public static class GeoLocationHttpContextKeys
{
    public const string ResolvedGeoLocation = "Security.ResolvedGeoLocation";
}
