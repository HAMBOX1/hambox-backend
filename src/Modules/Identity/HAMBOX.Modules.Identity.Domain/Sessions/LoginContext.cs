namespace HAMBOX.Modules.Identity.Domain.Sessions;

/// <summary>
/// The enrichment signals resolved for a login attempt — geolocation and parsed client info —
/// bundled so <see cref="LoginHistory"/>, <see cref="UserSession"/>, and
/// <see cref="TrustedDevice"/> all accept the same shape instead of repeating five positional
/// parameters at every call site.
/// </summary>
public sealed record LoginContext(
    string? CountryCode = null,
    string? City = null,
    string? BrowserName = null,
    string? OsName = null,
    string? DeviceType = null,
    string? Fingerprint = null);
