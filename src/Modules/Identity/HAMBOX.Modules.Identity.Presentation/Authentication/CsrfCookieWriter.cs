using System.Security.Cryptography;
using System.Text;
using HAMBOX.Modules.Identity.Application.Abstractions;
using HAMBOX.Modules.Identity.Application.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;

namespace HAMBOX.Modules.Identity.Presentation.Authentication;

/// <summary>
/// A plain double-submit-cookie CSRF defense for the two endpoints that authenticate via the
/// ambient refresh cookie (<c>refresh</c>, <c>logout</c>) — the only endpoints where a cross-site
/// page could otherwise ride the victim's browser's cookie without needing to read anything. The
/// token is non-HttpOnly (so Angular's built-in <c>withXsrfConfiguration</c> interceptor can read
/// it and echo it back as a header automatically): a cross-site attacker's page can trigger the
/// cookie to be *sent*, but — same-origin policy — cannot *read* it to also set the matching
/// header, which is exactly what this defends against. Deliberately not
/// <c>Microsoft.AspNetCore.Antiforgery</c>: that service's cookie and header values are a
/// cryptographically-paired pair, not equal strings, which Angular's generic double-submit
/// interceptor can't produce. Reuses <see cref="ITokenGenerator"/> — the same abstraction already
/// used for refresh/reset/verification tokens — rather than a new randomness source.
/// </summary>
public static class CsrfCookieWriter
{
    public const string CookieName = "XSRF-TOKEN";
    public const string HeaderName = "X-XSRF-TOKEN";

    /// <summary>
    /// Issues (or re-issues) the CSRF cookie alongside a refresh-token cookie write. Shares the
    /// refresh cookie's SameSite/Secure/Domain policy (same settings object) but deliberately
    /// overrides Path to "/" (not the refresh cookie's narrow "/api/auth"): unlike the HttpOnly
    /// refresh cookie, this one must be readable via <c>document.cookie</c> by Angular's
    /// <c>withXsrfConfiguration</c> interceptor from whatever SPA route the user is currently on
    /// (e.g. "/admin/login", "/account/dashboard") — a cookie scoped to "/api/auth" is invisible to
    /// JS on every page the SPA actually renders, since that scoping is relative to the *document's*
    /// path, not the destination of the XHR it's eventually attached to. Deliberately <b>not</b>
    /// HttpOnly.
    /// </summary>
    public static void WriteCsrfCookie(
        HttpContext httpContext,
        RefreshCookieSettings cookieSettings,
        IHostEnvironment environment,
        ITokenGenerator tokenGenerator)
    {
        var options = AuthCookieWriter.BuildCookieOptions(cookieSettings, environment, DateTimeOffset.UtcNow.AddDays(1));
        options.HttpOnly = false;
        options.Path = "/";
        httpContext.Response.Cookies.Append(CookieName, tokenGenerator.GenerateSecureToken(), options);
    }

    public static void ClearCsrfCookie(HttpContext httpContext, RefreshCookieSettings cookieSettings, IHostEnvironment environment)
    {
        var options = AuthCookieWriter.BuildCookieOptions(cookieSettings, environment, DateTimeOffset.UnixEpoch);
        options.HttpOnly = false;
        options.Path = "/";
        httpContext.Response.Cookies.Delete(CookieName, options);
    }

    /// <summary>
    /// True when the request carries both the cookie and the matching header (constant-time
    /// compare — the token is high-entropy so this isn't a critical timing surface, but it's cheap
    /// to do correctly).
    /// </summary>
    public static bool ValidateCsrfToken(HttpContext httpContext)
    {
        if (!httpContext.Request.Cookies.TryGetValue(CookieName, out var cookieValue)
            || string.IsNullOrWhiteSpace(cookieValue))
        {
            return false;
        }

        var headerValue = httpContext.Request.Headers[HeaderName].ToString();
        if (string.IsNullOrWhiteSpace(headerValue))
        {
            return false;
        }

        var cookieBytes = Encoding.UTF8.GetBytes(cookieValue);
        var headerBytes = Encoding.UTF8.GetBytes(headerValue);
        return cookieBytes.Length == headerBytes.Length && CryptographicOperations.FixedTimeEquals(cookieBytes, headerBytes);
    }
}
