using System.Net;
using System.Net.Http.Json;
using HAMBOX.IntegrationTests.RateLimiting;
using HAMBOX.Modules.Identity.Application.Abstractions;
using HAMBOX.Modules.Identity.Domain.Users;
using HAMBOX.Modules.Identity.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace HAMBOX.IntegrationTests.Auth;

/// <summary>
/// End-to-end coverage for the cookie-based refresh-token redesign, against the real host (real
/// <c>AuthEndpoints.cs</c>, real <c>RefreshTokenCommandHandler</c> rotation/reuse-detection — this
/// suite proves the transport-layer change wired correctly onto business logic that was already
/// correct before this change, not the rotation logic itself). Reuses
/// <see cref="HamboxRateLimitWebApplicationFactory"/>; the client's <c>BaseAddress</c> is forced to
/// <c>https://</c> so the automatic cookie jar honors the refresh cookie's <c>Secure</c> flag (the
/// "Testing" hosting environment sets <c>Secure=true</c>, same as any non-Development environment —
/// see <c>AuthCookieWriter</c>) exactly like a real browser would.
/// </summary>
[Collection(HamboxApiFactoryCollection.Name)]
public sealed class CookieAuthFlowTests : IAsyncLifetime
{
    private const string Password = "TestPassword123!";
    private const string RefreshCookieName = "hambox_rt";
    private const string CsrfCookieName = "XSRF-TOKEN";
    private const string CsrfHeaderName = "X-XSRF-TOKEN";

    private readonly HamboxRateLimitWebApplicationFactory _factory = new();
    private HttpClient _client = null!;
    private string _email = null!;

    public async Task InitializeAsync()
    {
        await _factory.InitializeIdentitySchemaAsync();
        // CreateDefaultClient (not CreateClient) — no automatic cookie-jar handling, so every
        // request's cookie/CSRF-header values come exclusively from what each test explicitly sets
        // via BuildRequest. An automatic jar would otherwise silently overwrite a deliberately-stale
        // cookie (the reuse-detection tests) with whatever it currently holds.
        _client = _factory.CreateDefaultClient(new Uri("https://localhost"));
        // Every real browser sends a User-Agent; LoginCommandHandler's LoginHistory recording
        // requires a non-empty one (a pre-existing, unrelated behavior — out of scope to change here).
        _client.DefaultRequestHeaders.Add("User-Agent", "HAMBOX-IntegrationTests/1.0");
        _email = $"cookie-auth-{Guid.NewGuid():N}@example.com";
        await SeedActiveUserAsync(_email);
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DropDatabaseAsync();
        await _factory.DisposeAsync();
    }

    private async Task SeedActiveUserAsync(string email)
    {
        using var scope = _factory.Services.CreateScope();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();

        var user = ApplicationUser.Create(email, hasher.HashPassword(Password), "Test", "User");
        user.ConfirmEmail();
        user.Activate();
        db.Users.Add(user);
        await db.SaveChangesAsync();
    }

    private Task<HttpResponseMessage> LoginAsync() =>
        _client.PostAsJsonAsync("api/auth/login", new { Email = _email, Password, RememberMe = false });

    /// <summary>Extracts one cookie's value from a response's raw Set-Cookie header(s).</summary>
    private static string? ExtractSetCookieValue(HttpResponseMessage response, string cookieName)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var setCookieHeaders))
        {
            return null;
        }

        foreach (var header in setCookieHeaders)
        {
            if (header.StartsWith(cookieName + "=", StringComparison.OrdinalIgnoreCase))
            {
                var afterName = header[(cookieName.Length + 1)..];
                return afterName.Split(';')[0];
            }
        }

        return null;
    }

    private static bool WasCookieCleared(HttpResponseMessage response, string cookieName)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var setCookieHeaders))
        {
            return false;
        }

        foreach (var header in setCookieHeaders)
        {
            if (header.StartsWith(cookieName + "=", StringComparison.OrdinalIgnoreCase)
                && (header.Contains("expires=Thu, 01 Jan 1970", StringComparison.OrdinalIgnoreCase)
                    || header.Contains("expires=Thu, 01-Jan-1970", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Builds a refresh/logout request carrying an explicit, manually-set refresh cookie (bypassing
    /// the client's automatic cookie jar) plus the matching CSRF header — used for the negative
    /// reuse/invalid-token tests, which must present a *specific* (often stale) cookie value rather
    /// than whatever the jar currently holds.
    /// </summary>
    private HttpRequestMessage BuildRequest(HttpMethod method, string path, string? refreshCookieValue, string? csrfValue)
    {
        var request = new HttpRequestMessage(method, path);

        // The CSRF check reads the token from BOTH the cookie and the header (that's the
        // double-submit pattern) — the cookie must be sent here too, not just echoed as a header,
        // mirroring exactly what a real browser (and Angular's withXsrfConfiguration) would send.
        var cookiePairs = new List<string>();
        if (refreshCookieValue is not null)
        {
            cookiePairs.Add($"{RefreshCookieName}={refreshCookieValue}");
        }

        if (csrfValue is not null)
        {
            cookiePairs.Add($"{CsrfCookieName}={csrfValue}");
        }

        if (cookiePairs.Count > 0)
        {
            request.Headers.Add("Cookie", string.Join("; ", cookiePairs));
        }

        if (csrfValue is not null)
        {
            request.Headers.Add(CsrfHeaderName, csrfValue);
        }

        return request;
    }

    [Fact]
    public async Task Login_SetsHttpOnlySecureRefreshCookie_AndBodyNeverContainsTheRefreshToken()
    {
        using var response = await LoginAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var setCookie = ExtractSetCookieValue(response, RefreshCookieName);
        Assert.NotNull(setCookie);

        var rawHeaders = string.Join('\n', response.Headers.TryGetValues("Set-Cookie", out var v) ? v : []);
        Assert.Contains("httponly", rawHeaders, StringComparison.OrdinalIgnoreCase);
        // "Testing" is a non-Development environment, so AuthCookieWriter sets Secure=true here —
        // same code path a real Production deployment uses.
        Assert.Contains("secure", rawHeaders, StringComparison.OrdinalIgnoreCase);

        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("refreshToken", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("accessToken", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Login_AlsoIssuesTheCsrfCookie_ReadableNotHttpOnly()
    {
        using var response = await LoginAsync();

        var rawHeaders = response.Headers.TryGetValues("Set-Cookie", out var v) ? v.ToList() : [];
        var csrfHeader = Assert.Single(rawHeaders.Where(h => h.StartsWith(CsrfCookieName + "=", StringComparison.OrdinalIgnoreCase)));
        Assert.DoesNotContain("httponly", csrfHeader, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Login_CsrfCookie_IsScopedToRootPath_NotTheNarrowRefreshCookiePath()
    {
        // The refresh cookie can stay scoped to /api/auth (the browser only needs to *send* it
        // there). The CSRF cookie must be readable via document.cookie from every SPA route
        // (/admin/login, /account/dashboard, ...) so Angular's withXsrfConfiguration interceptor
        // can echo it back as a header — document.cookie visibility follows the *page's* path
        // against the cookie's Path, not the destination of the eventual XHR, so a cookie scoped
        // to /api/auth would be invisible to JS on every real page the SPA renders.
        using var response = await LoginAsync();

        var rawHeaders = response.Headers.TryGetValues("Set-Cookie", out var v) ? v.ToList() : [];
        var csrfHeader = Assert.Single(rawHeaders.Where(h => h.StartsWith(CsrfCookieName + "=", StringComparison.OrdinalIgnoreCase)));
        var refreshHeader = Assert.Single(rawHeaders.Where(h => h.StartsWith(RefreshCookieName + "=", StringComparison.OrdinalIgnoreCase)));

        Assert.Contains("path=/;", csrfHeader, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/api/auth", refreshHeader, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Refresh_WithValidCookieAndCsrfHeader_RotatesAndIssuesNewAccessToken()
    {
        using var loginResponse = await LoginAsync();
        var originalRefreshCookie = ExtractSetCookieValue(loginResponse, RefreshCookieName);
        var csrfValue = ExtractSetCookieValue(loginResponse, CsrfCookieName);
        var originalBody = await loginResponse.Content.ReadFromJsonAsync<AccessTokenBody>();

        using var refreshRequest = BuildRequest(HttpMethod.Post, "api/auth/refresh", originalRefreshCookie, csrfValue);
        using var refreshResponse = await _client.SendAsync(refreshRequest);

        Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);
        var newRefreshCookie = ExtractSetCookieValue(refreshResponse, RefreshCookieName);
        var newBody = await refreshResponse.Content.ReadFromJsonAsync<AccessTokenBody>();

        Assert.NotNull(newRefreshCookie);
        Assert.NotEqual(originalRefreshCookie, newRefreshCookie); // rotated
        Assert.NotEqual(originalBody!.AccessToken, newBody!.AccessToken);

        var refreshBody = await refreshResponse.Content.ReadAsStringAsync();
        Assert.DoesNotContain("refreshToken", refreshBody, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Refresh_TwoSimultaneousRequestsWithTheSameActiveToken_OnlyOneSucceeds()
    {
        // Two browser tabs booting at once (or an attacker racing a stolen-but-not-yet-rotated
        // cookie) both present the SAME still-active refresh token concurrently. The RowVersion
        // concurrency token on RefreshToken must make exactly one of these two truly-parallel
        // requests win; the other must fail outright — never both succeeding with two live
        // children issued from one now-revoked parent.
        using var loginResponse = await LoginAsync();
        var originalRefreshCookie = ExtractSetCookieValue(loginResponse, RefreshCookieName);
        var csrfValue = ExtractSetCookieValue(loginResponse, CsrfCookieName);

        using var firstRequest = BuildRequest(HttpMethod.Post, "api/auth/refresh", originalRefreshCookie, csrfValue);
        using var secondRequest = BuildRequest(HttpMethod.Post, "api/auth/refresh", originalRefreshCookie, csrfValue);

        var firstTask = _client.SendAsync(firstRequest);
        var secondTask = _client.SendAsync(secondRequest);
        await Task.WhenAll(firstTask, secondTask);

        using var firstResponse = await firstTask;
        using var secondResponse = await secondTask;

        var statusCodes = new[] { firstResponse.StatusCode, secondResponse.StatusCode };
        Assert.Single(statusCodes, HttpStatusCode.OK);
        Assert.Single(statusCodes, code => code != HttpStatusCode.OK);

        // The race must not be silently mistaken for reuse-detection's account-wide revocation —
        // it's a benign concurrent race, so the winner's freshly-issued access token must still
        // authorize normally afterward (a WasReused()-triggered mass-revoke would break this).
        var winner = firstResponse.StatusCode == HttpStatusCode.OK ? firstResponse : secondResponse;
        var winnerBody = await winner.Content.ReadFromJsonAsync<AccessTokenBody>();

        using var meRequest = new HttpRequestMessage(HttpMethod.Get, "api/auth/me");
        meRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", winnerBody!.AccessToken);
        using var meResponse = await _client.SendAsync(meRequest);

        Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);
    }

    [Fact]
    public async Task Refresh_ReusedOldToken_IsRejected_AndCascadesToRevokeTheRotatedSuccessor()
    {
        using var loginResponse = await LoginAsync();
        var originalRefreshCookie = ExtractSetCookieValue(loginResponse, RefreshCookieName);
        var csrfValue = ExtractSetCookieValue(loginResponse, CsrfCookieName);

        using var firstRefreshRequest = BuildRequest(HttpMethod.Post, "api/auth/refresh", originalRefreshCookie, csrfValue);
        using var firstRefreshResponse = await _client.SendAsync(firstRefreshRequest);
        var rotatedRefreshCookie = ExtractSetCookieValue(firstRefreshResponse, RefreshCookieName);
        var rotatedCsrfValue = ExtractSetCookieValue(firstRefreshResponse, CsrfCookieName) ?? csrfValue;

        // Reuse the ORIGINAL (already-rotated-away) token — this is the stolen-cookie/replay scenario.
        using var reuseRequest = BuildRequest(HttpMethod.Post, "api/auth/refresh", originalRefreshCookie, csrfValue);
        using var reuseResponse = await _client.SendAsync(reuseRequest);
        Assert.Equal(HttpStatusCode.BadRequest, reuseResponse.StatusCode);

        // Reuse detection must cascade: the legitimately-rotated successor token is now revoked too,
        // per RefreshTokenCommandHandler's existing (untouched) RevokeAllUserTokensAsync behavior.
        using var rotatedRequest = BuildRequest(HttpMethod.Post, "api/auth/refresh", rotatedRefreshCookie, rotatedCsrfValue);
        using var rotatedResponse = await _client.SendAsync(rotatedRequest);
        Assert.Equal(HttpStatusCode.BadRequest, rotatedResponse.StatusCode);
    }

    [Fact]
    public async Task Refresh_InvalidGarbageCookie_IsRejected()
    {
        using var loginResponse = await LoginAsync();
        var csrfValue = ExtractSetCookieValue(loginResponse, CsrfCookieName);

        using var request = BuildRequest(HttpMethod.Post, "api/auth/refresh", "this-is-not-a-real-token", csrfValue);
        using var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Refresh_NoCookiePresent_IsUnauthorized()
    {
        using var response = await _client.PostAsync("api/auth/refresh", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Refresh_ValidCookieButMissingCsrfHeader_IsForbidden()
    {
        using var loginResponse = await LoginAsync();
        var refreshCookie = ExtractSetCookieValue(loginResponse, RefreshCookieName);

        using var request = BuildRequest(HttpMethod.Post, "api/auth/refresh", refreshCookie, csrfValue: null);
        using var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Logout_ClearsBothCookies_AndSubsequentRefreshWithTheOldCookieFails()
    {
        using var loginResponse = await LoginAsync();
        var refreshCookie = ExtractSetCookieValue(loginResponse, RefreshCookieName);
        var csrfValue = ExtractSetCookieValue(loginResponse, CsrfCookieName);

        using var logoutRequest = BuildRequest(HttpMethod.Post, "api/auth/logout", refreshCookie, csrfValue);
        using var logoutResponse = await _client.SendAsync(logoutRequest);

        Assert.Equal(HttpStatusCode.OK, logoutResponse.StatusCode);
        Assert.True(WasCookieCleared(logoutResponse, RefreshCookieName));
        Assert.True(WasCookieCleared(logoutResponse, CsrfCookieName));

        using var refreshAfterLogoutRequest = BuildRequest(HttpMethod.Post, "api/auth/refresh", refreshCookie, csrfValue);
        using var refreshAfterLogoutResponse = await _client.SendAsync(refreshAfterLogoutRequest);
        Assert.Equal(HttpStatusCode.BadRequest, refreshAfterLogoutResponse.StatusCode);
    }

    [Fact]
    public async Task Logout_NoCookiePresent_IsIdempotentSuccess()
    {
        using var response = await _client.PostAsync("api/auth/logout", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task AuthorizationStillWorks_WithTheAccessTokenIssuedByRefresh()
    {
        using var loginResponse = await LoginAsync();
        var refreshCookie = ExtractSetCookieValue(loginResponse, RefreshCookieName);
        var csrfValue = ExtractSetCookieValue(loginResponse, CsrfCookieName);

        using var refreshRequest = BuildRequest(HttpMethod.Post, "api/auth/refresh", refreshCookie, csrfValue);
        using var refreshResponse = await _client.SendAsync(refreshRequest);
        var body = await refreshResponse.Content.ReadFromJsonAsync<AccessTokenBody>();

        using var meRequest = new HttpRequestMessage(HttpMethod.Get, "api/auth/me");
        meRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", body!.AccessToken);
        using var meResponse = await _client.SendAsync(meRequest);

        Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);
    }

    private sealed record AccessTokenBody(string AccessToken, DateTimeOffset ExpiresAt);
}
