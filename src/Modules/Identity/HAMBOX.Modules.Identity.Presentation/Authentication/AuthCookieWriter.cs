using HAMBOX.Modules.Identity.Application.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;

namespace HAMBOX.Modules.Identity.Presentation.Authentication;

/// <summary>
/// Writes/clears the HttpOnly refresh-token cookie. <see cref="BuildCookieOptions"/> is a pure
/// function (environment + settings → <see cref="CookieOptions"/>) so the Secure/SameSite/Path
/// logic is directly unit-testable without booting the app — mirrors
/// <c>InfrastructureExtensions.ConfigureHamboxCorsPolicy</c>'s extraction for the same reason.
/// </summary>
public static class AuthCookieWriter
{
    /// <summary>
    /// Builds the <see cref="CookieOptions"/> for the refresh-token cookie. <c>Secure</c> follows
    /// the same <c>!IsDevelopment()</c> gate <c>Program.cs</c> already uses for
    /// <c>UseHsts</c>/<c>UseHttpsRedirection</c> — a <c>Secure</c> cookie set over plain
    /// <c>http://localhost</c> in local dev would simply never reach the browser.
    /// </summary>
    public static CookieOptions BuildCookieOptions(
        RefreshCookieSettings settings, IHostEnvironment environment, DateTimeOffset expiresOnUtc) =>
        new()
        {
            HttpOnly = true,
            Secure = !environment.IsDevelopment(),
            SameSite = ParseSameSite(settings.SameSite),
            Path = settings.Path,
            Domain = string.IsNullOrWhiteSpace(settings.Domain) ? null : settings.Domain,
            Expires = expiresOnUtc,
        };

    /// <summary>
    /// Sets the refresh-token cookie, expiring with the refresh token's own lifetime (including
    /// the remember-me case — <paramref name="refreshExpiresOnUtc"/> is the real expiry computed
    /// by <c>AuthTokenIssuer</c>/<c>RefreshTokenCommandHandler</c>, not a fixed default).
    /// </summary>
    public static void WriteRefreshTokenCookie(
        HttpContext httpContext,
        RefreshCookieSettings settings,
        IHostEnvironment environment,
        string refreshTokenPlaintext,
        DateTimeOffset refreshExpiresOnUtc)
    {
        var options = BuildCookieOptions(settings, environment, refreshExpiresOnUtc);
        httpContext.Response.Cookies.Append(settings.CookieName, refreshTokenPlaintext, options);
    }

    /// <summary>
    /// Clears the refresh-token cookie. The options passed to <c>Delete</c> (Path/Domain/SameSite/
    /// Secure) must match what <see cref="WriteRefreshTokenCookie"/> used when setting it, or the
    /// browser won't recognize it as the same cookie to remove.
    /// </summary>
    public static void ClearRefreshTokenCookie(
        HttpContext httpContext, RefreshCookieSettings settings, IHostEnvironment environment)
    {
        var options = BuildCookieOptions(settings, environment, DateTimeOffset.UnixEpoch);
        httpContext.Response.Cookies.Delete(settings.CookieName, options);
    }

    private static SameSiteMode ParseSameSite(string value) => value.Trim().ToLowerInvariant() switch
    {
        "strict" => SameSiteMode.Strict,
        "none" => SameSiteMode.None,
        _ => SameSiteMode.Lax,
    };
}
