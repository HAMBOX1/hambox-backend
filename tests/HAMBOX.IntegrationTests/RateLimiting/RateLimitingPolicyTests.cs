using System.Net;
using System.Net.Http.Json;

namespace HAMBOX.IntegrationTests.RateLimiting;

/// <summary>
/// Regression coverage for the ASP.NET Core rate-limiting policies registered in
/// <c>HAMBOX.API/Program.cs</c> (Commerce's <c>CheckoutInitiation</c>/<c>PaymentCallback</c>) and
/// <c>IdentityInfrastructureExtensions</c> (<c>RateLimitPolicies.Login</c>), against the real
/// endpoints they're attached to (<c>AuthEndpoints</c>, <c>CartEndpoints</c>,
/// <c>DotFawryPaymentEndpoints</c>).
///
/// xUnit creates a fresh instance of this class per <see cref="Fact"/>, so each test gets its own
/// <see cref="HamboxRateLimitWebApplicationFactory"/> — a fresh app instance with fresh in-process
/// rate limiter state — never sharing a bucket with another test. This proves each policy's exact
/// configured limit (not just "some" limiting exists): if a future change loosens/removes a
/// policy's <c>PermitLimit</c>, or detaches <c>.RequireRateLimiting(...)</c> from the endpoint, the
/// corresponding test below fails.
/// </summary>
[Collection(HamboxApiFactoryCollection.Name)]
public sealed class RateLimitingPolicyTests : IAsyncLifetime
{
    private readonly HamboxRateLimitWebApplicationFactory _factory = new();
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        await _factory.InitializeIdentitySchemaAsync();
        await _factory.DisableMaintenanceModeAsync();
        _client = _factory.CreateClient();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DropDatabaseAsync();
        await _factory.DisposeAsync();
    }

    /// <summary>
    /// <c>POST api/auth/maintenance-bypass</c> is protected by <c>RateLimitPolicies.Login</c>
    /// (5 requests / 60s, per appsettings.json's <c>RateLimiting:Login</c> section). No wrong
    /// password ever authenticates, so every request that clears the limiter reaches
    /// <c>VerifyMaintenanceBypassCommandHandler</c> (a real DB-backed password check against the
    /// migrated, empty scratch database) and comes back 400 — proving the limiter passed it all the
    /// way through to genuine business validation, not merely "not 429".
    /// </summary>
    [Fact]
    public async Task MaintenanceBypass_AllowsFiveRequests_ThenBlocksSixthWith429()
    {
        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 6; i++)
        {
            using var response = await _client.PostAsJsonAsync(
                "api/auth/maintenance-bypass",
                new { Password = "definitely-wrong-password" });
            statuses.Add(response.StatusCode);
        }

        Assert.Equal(
            [HttpStatusCode.BadRequest, HttpStatusCode.BadRequest, HttpStatusCode.BadRequest,
             HttpStatusCode.BadRequest, HttpStatusCode.BadRequest, HttpStatusCode.TooManyRequests],
            statuses);
    }

    /// <summary>
    /// <c>POST api/v1/checkout</c> is protected by
    /// <c>CommerceRateLimitPolicies.CheckoutInitiation</c> (20 requests / 60s, hardcoded in
    /// Program.cs). No Authorization header is attached, so every request the limiter admits is
    /// rejected 401 by ASP.NET Core authorization (the endpoint's <c>.RequireAuthorization()</c> +
    /// <c>.RequireCustomerContext()</c>) — real request processing beyond the limiter, without
    /// needing a real customer session, JWT, or payment gateway. Because <c>UseRateLimiter()</c> is
    /// registered before <c>UseAuthentication()</c>/<c>UseAuthorization()</c> in the pipeline, the
    /// limiter's own verdict (429 on #21) is unaffected by the auth outcome.
    /// </summary>
    [Fact]
    public async Task Checkout_AllowsTwentyRequests_ThenBlocksTwentyFirstWith429()
    {
        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 21; i++)
        {
            using var response = await _client.PostAsJsonAsync(
                "api/v1/checkout",
                new { Email = "shopper@example.com", Country = "US", PaymentMethod = "Development" });
            statuses.Add(response.StatusCode);
        }

        Assert.All(statuses.Take(20), status => Assert.Equal(HttpStatusCode.Unauthorized, status));
        Assert.Equal(HttpStatusCode.TooManyRequests, statuses[20]);
    }

    /// <summary>
    /// <c>POST api/v1/payments/dot-fawry/notify</c> is protected by
    /// <c>CommerceRateLimitPolicies.PaymentCallback</c> (30 requests / 60s). It is anonymous, so —
    /// unlike checkout — nothing except the endpoint's own IP allowlist stands between the limiter
    /// and <c>HandleDotFawryNotificationCommand</c>. TestServer's remote IP never matches DOT's
    /// hardcoded allowlist (<c>DotNotificationIpAllowlist</c>), so every request the limiter admits
    /// reaches that handler-adjacent check and is rejected 403 — it never reaches the mediator
    /// dispatch. That the first 30 responses are 403 (the handler ran and rejected them) and only
    /// #31 is 429 (the handler never ran at all) is the proof that the limiter executes before the
    /// handler, not the other way around.
    /// </summary>
    [Fact]
    public async Task DotFawryNotify_LimiterRunsBeforeHandler_AllowsThirtyThenBlocksThirtyFirstWith429()
    {
        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 31; i++)
        {
            using var response = await _client.PostAsJsonAsync(
                "api/v1/payments/dot-fawry/notify",
                new
                {
                    DotTransId = "T-1",
                    PartnerTransId = "P-1",
                    OpId = 1,
                    Msisdn = "0100000000",
                    Amount = "10.00",
                    ServiceId = "S-1",
                    ResultCode = "0",
                    ResultDesc = "OK",
                });
            statuses.Add(response.StatusCode);
        }

        Assert.All(statuses.Take(30), status => Assert.Equal(HttpStatusCode.Forbidden, status));
        Assert.Equal(HttpStatusCode.TooManyRequests, statuses[30]);
    }

    /// <summary>
    /// <c>POST api/auth/refresh</c> is protected by <c>RateLimitPolicies.Refresh</c>
    /// (30 requests / 60s). No cookie is attached, so every request the limiter admits reaches the
    /// endpoint's own missing-cookie check and is rejected 401 — proving the limiter gates the
    /// endpoint itself, not just its business logic.
    /// </summary>
    [Fact]
    public async Task Refresh_AllowsThirtyRequests_ThenBlocksThirtyFirstWith429()
    {
        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 31; i++)
        {
            using var response = await _client.PostAsync("api/auth/refresh", null);
            statuses.Add(response.StatusCode);
        }

        Assert.All(statuses.Take(30), status => Assert.Equal(HttpStatusCode.Unauthorized, status));
        Assert.Equal(HttpStatusCode.TooManyRequests, statuses[30]);
    }

    /// <summary>
    /// <c>POST api/auth/logout</c> shares <c>RateLimitPolicies.Refresh</c> with <c>refresh</c>. No
    /// cookie is attached, so every request the limiter admits reaches the endpoint's idempotent
    /// "nothing to log out" path and returns 200 — proving the limiter is attached even though the
    /// endpoint itself never fails for a missing cookie.
    /// </summary>
    [Fact]
    public async Task Logout_AllowsThirtyRequests_ThenBlocksThirtyFirstWith429()
    {
        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 31; i++)
        {
            using var response = await _client.PostAsync("api/auth/logout", null);
            statuses.Add(response.StatusCode);
        }

        Assert.All(statuses.Take(30), status => Assert.Equal(HttpStatusCode.OK, status));
        Assert.Equal(HttpStatusCode.TooManyRequests, statuses[30]);
    }
}
