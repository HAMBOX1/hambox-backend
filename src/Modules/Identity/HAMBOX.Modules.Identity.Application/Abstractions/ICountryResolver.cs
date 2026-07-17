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
    /// Resolves the ISO-3166 alpha-2 country code for the given request signals, or null if it
    /// cannot be determined.
    /// </summary>
    Task<string?> ResolveCountryAsync(CountryResolutionRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// The raw per-request signals available to a country resolver: the client IP address and the
/// full request header collection, so a header-based resolver can read whichever header its
/// provider uses without requiring an interface change.
/// </summary>
public sealed record CountryResolutionRequest(string? IpAddress, IReadOnlyDictionary<string, string> Headers);
