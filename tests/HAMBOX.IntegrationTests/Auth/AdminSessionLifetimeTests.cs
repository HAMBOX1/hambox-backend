using System.Net;
using System.Net.Http.Json;
using HAMBOX.IntegrationTests.RateLimiting;
using HAMBOX.Modules.Identity.Domain.Sessions;
using HAMBOX.Modules.Identity.Infrastructure.Persistence;
using HAMBOX.Modules.Identity.Presentation.Endpoints;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HAMBOX.IntegrationTests.Auth;

/// <summary>
/// Proves the P1 backend-authoritative admin session lifetime fixes against the real host (real
/// <c>RefreshTokenCommandHandler</c>, <c>SessionValidator</c>): an idle admin session, or one older
/// than the absolute max lifetime, can no longer be extended by refreshing — both require a fresh
/// Email+Password+OTP login — and a still-unexpired access token for such a session is rejected in
/// real time on its very next request, not just at the next refresh attempt. Neither check ever
/// applies to the customer/storefront auth context (proven separately by <see cref="CookieAuthFlowTests"/>
/// continuing to pass unchanged).
/// </summary>
[Collection(HamboxApiFactoryCollection.Name)]
public sealed class AdminSessionLifetimeTests : IAsyncLifetime
{
    private const string RefreshCookieName = "hambox_rt";
    private const string CsrfCookieName = "XSRF-TOKEN";

    private readonly HamboxRateLimitWebApplicationFactory _factory = new();
    private string _adminEmail = null!;
    private Guid _adminUserId;

    public async Task InitializeAsync()
    {
        await _factory.InitializeIdentitySchemaAsync();
        _adminEmail = $"admin-session-test-{Guid.NewGuid():N}@example.com";
        _adminUserId = await AdminUserSeeder.SeedAsync(_factory.Services, _adminEmail);
        // OTP disabled purely to get an immediately-authenticated session with one HTTP call — these
        // tests are about session lifetime after login, not the OTP flow itself (covered separately).
        await PlatformSettingsTestHelpers.SetAdminOtpEnabledAsync(_factory.Services, false);
    }

    public async Task DisposeAsync()
    {
        await _factory.DropDatabaseAsync();
        await _factory.DisposeAsync();
    }

    private HttpClient CreateClient()
    {
        var client = _factory.CreateDefaultClient(new Uri("https://localhost"));
        client.DefaultRequestHeaders.Add("User-Agent", "HAMBOX-IntegrationTests/1.0");
        return client;
    }

    private async Task<(string AccessToken, string RefreshCookie, string Csrf)> LoginAsync(HttpClient client)
    {
        using var response = await client.PostAsJsonAsync(
            "api/auth/admin/login", new { Email = _adminEmail, Password = AdminUserSeeder.Password });
        var body = await response.Content.ReadFromJsonAsync<AdminLoginChallengeResponseDto>();
        return (
            body!.Token!.AccessToken,
            ExtractSetCookieValue(response, RefreshCookieName)!,
            ExtractSetCookieValue(response, CsrfCookieName)!);
    }

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
                return header[(cookieName.Length + 1)..].Split(';')[0];
            }
        }

        return null;
    }

    private HttpRequestMessage BuildRefreshRequest(string refreshCookie, string csrf)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "api/auth/refresh");
        request.Headers.Add("Cookie", $"{RefreshCookieName}={refreshCookie}; {CsrfCookieName}={csrf}");
        request.Headers.Add("X-XSRF-TOKEN", csrf);
        return request;
    }

    /// <summary>Directly backdates the admin's single active session row — the only way to simulate
    /// "idle for 31 minutes" or "logged in 9 hours ago" without a real clock wait. Uses EF's
    /// property-by-name API so the domain entity's intentionally-private setters stay private.</summary>
    private async Task BackdateSessionAsync(DateTimeOffset? lastActivityOnUtc, DateTimeOffset? startedOnUtc)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var session = await db.UserSessions
            .OrderByDescending(s => s.StartedOnUtc)
            .FirstAsync(s => s.UserId == _adminUserId);

        var entry = db.Entry(session);
        if (lastActivityOnUtc.HasValue)
        {
            entry.Property(nameof(UserSession.LastActivityOnUtc)).CurrentValue = lastActivityOnUtc.Value;
        }

        if (startedOnUtc.HasValue)
        {
            entry.Property(nameof(UserSession.StartedOnUtc)).CurrentValue = startedOnUtc.Value;
        }

        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Refresh_FreshAdminSession_SucceedsNormally()
    {
        using var client = CreateClient();
        var (_, refreshCookie, csrf) = await LoginAsync(client);

        using var response = await client.SendAsync(BuildRefreshRequest(refreshCookie, csrf));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Refresh_AdminSessionIdleBeyondTimeout_IsRejected_AndRequiresFreshLogin()
    {
        using var client = CreateClient();
        var (_, refreshCookie, csrf) = await LoginAsync(client);

        // Default AdminIdleTimeoutMinutes is 30 — simulate 31 minutes of no activity.
        await BackdateSessionAsync(lastActivityOnUtc: DateTimeOffset.UtcNow.AddMinutes(-31), startedOnUtc: null);

        using var response = await client.SendAsync(BuildRefreshRequest(refreshCookie, csrf));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Identity.AdminSessionExpired", body);
    }

    [Fact]
    public async Task Refresh_AdminSessionOlderThanMaxLifetime_IsRejected_EvenWhenRecentlyActive()
    {
        using var client = CreateClient();
        var (_, refreshCookie, csrf) = await LoginAsync(client);

        // Default AdminMaxSessionLifetimeHours is 8 — simulate a session started 9 hours ago, but
        // with activity moments ago (the "active every few minutes" scenario from the requirement).
        await BackdateSessionAsync(
            lastActivityOnUtc: DateTimeOffset.UtcNow.AddSeconds(-5),
            startedOnUtc: DateTimeOffset.UtcNow.AddHours(-9));

        using var response = await client.SendAsync(BuildRefreshRequest(refreshCookie, csrf));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Identity.AdminSessionExpired", body);
    }

    [Fact]
    public async Task StillUnexpiredAccessToken_ForAnIdleExpiredSession_IsRejectedInRealTime()
    {
        using var client = CreateClient();
        var (accessToken, _, _) = await LoginAsync(client);

        await BackdateSessionAsync(lastActivityOnUtc: DateTimeOffset.UtcNow.AddMinutes(-31), startedOnUtc: null);

        // The access token itself hasn't expired yet — SessionValidator's real-time check (run on
        // every authenticated request via OnTokenValidated) must still reject it immediately.
        using var meRequest = new HttpRequestMessage(HttpMethod.Get, "api/auth/me");
        meRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        using var meResponse = await client.SendAsync(meRequest);

        Assert.Equal(HttpStatusCode.Unauthorized, meResponse.StatusCode);
    }
}
