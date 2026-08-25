using HAMBOX.IntegrationTests.RateLimiting;

namespace HAMBOX.IntegrationTests.Security;

/// <summary>
/// Regression coverage for a MEDIUM security fix: the API previously set no baseline security
/// response headers at all. Boots the real HAMBOX.API host (reusing
/// <see cref="HamboxRateLimitWebApplicationFactory"/> — see its own docs for why "Testing"
/// environment + a scratch LocalDB name are used) and checks the headers on a real response,
/// including one the app itself never reaches app-level routing to produce cleanly for (health
/// check against an unmigrated scratch DB) — proving <c>SecurityHeadersMiddleware</c> runs early
/// enough via <c>Response.OnStarting</c> to cover every response, not just the happy path.
/// </summary>
[Collection(HamboxApiFactoryCollection.Name)]
public sealed class SecurityHeadersTests : IAsyncLifetime
{
    private readonly HamboxRateLimitWebApplicationFactory _factory = new();
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        // Migrating first avoids a slow (~10s) SQL connection-timeout on the health check's
        // "Cannot open database" failure against a not-yet-created scratch database.
        await _factory.InitializeIdentitySchemaAsync();
        _client = _factory.CreateClient();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DropDatabaseAsync();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task AnyResponse_CarriesTheBaselineSecurityHeaders()
    {
        using var response = await _client.GetAsync("health");

        Assert.Equal("nosniff", GetHeader(response, "X-Content-Type-Options"));
        Assert.Equal("DENY", GetHeader(response, "X-Frame-Options"));
        Assert.Equal("strict-origin-when-cross-origin", GetHeader(response, "Referrer-Policy"));
    }

    private static string? GetHeader(HttpResponseMessage response, string name) =>
        response.Headers.TryGetValues(name, out var values) ? values.FirstOrDefault() : null;
}
