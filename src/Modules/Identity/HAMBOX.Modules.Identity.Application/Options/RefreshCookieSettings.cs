namespace HAMBOX.Modules.Identity.Application.Options;

/// <summary>
/// Settings for the HttpOnly cookie the refresh token is transported in. <c>Secure</c> and
/// <c>HttpOnly</c> are not configurable here — they are non-negotiable (Secure follows the same
/// <c>!IsDevelopment()</c> gate <c>Program.cs</c> already uses for <c>UseHsts</c>/<c>UseHttpsRedirection</c>,
/// HttpOnly is always true). Only the attributes that legitimately vary by deployment topology are
/// configuration.
/// </summary>
public sealed class RefreshCookieSettings
{
    /// <summary>
    /// The configuration section name.
    /// </summary>
    public const string SectionName = "Authentication:RefreshCookie";

    /// <summary>
    /// Gets the cookie name.
    /// </summary>
    public string CookieName { get; init; } = "hambox_rt";

    /// <summary>
    /// Gets the cookie path — scoped narrowly to the auth endpoints that actually read it, so it
    /// is never sent on ordinary API calls.
    /// </summary>
    public string Path { get; init; } = "/api/auth";

    /// <summary>
    /// Gets the SameSite policy: "Lax", "Strict", or "None". Same-site deployments (the documented/
    /// verified HAMBOX topology — frontend and API share an origin via a server-side reverse-proxy
    /// rewrite) should use "Lax". Only a genuinely cross-site deployment needs "None", which also
    /// requires <c>Secure</c> (always true outside Development) and is why CSRF defense is applied
    /// regardless of this setting rather than relying on SameSite alone.
    /// </summary>
    public string SameSite { get; init; } = "Lax";

    /// <summary>
    /// Gets the optional cookie <c>Domain</c> attribute. Left unset by default so the cookie is
    /// scoped to whichever host the browser actually contacted (correct for a reverse-proxy
    /// topology where the browser never sees the real API origin).
    /// </summary>
    public string? Domain { get; init; }
}
