using System.Net;
using System.Net.Http.Json;
using HAMBOX.IntegrationTests.RateLimiting;
using HAMBOX.Modules.Identity.Domain.Tokens;
using HAMBOX.Modules.Identity.Domain.Users;
using HAMBOX.Modules.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HAMBOX.IntegrationTests.Auth;

/// <summary>
/// Regression coverage for a security fix: <c>POST api/auth/verify-email</c> used to bind its token
/// from the query string (<c>[FromQuery]</c>), which <c>ApiRequestLoggingMiddleware</c> then
/// persisted verbatim (<c>Path + QueryString</c>) into <c>commerce.ApiRequestLogs</c> on every call.
/// The endpoint now binds <c>VerifyEmailRequest</c> via <c>[FromBody]</c> instead. Boots the real
/// HAMBOX.API host (see <see cref="HamboxRateLimitWebApplicationFactory"/>) to prove the token
/// travels in the body end-to-end, that a query-string-only token no longer works, and that the
/// endpoint carries the same <c>AccountActions</c> rate limit as its sibling account-action
/// endpoints (forgot-password/reset-password/resend-verification).
/// </summary>
[Collection(HamboxApiFactoryCollection.Name)]
public sealed class VerifyEmailEndpointTests : IAsyncLifetime
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

    private async Task<string> SeedVerifiableUserAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();

        // The default (Customer) role is already seeded by the Identity migrations themselves — do
        // not add a second one here, or it collides with IX_Roles_NormalizedName.
        var user = ApplicationUser.Create($"verify-{Guid.NewGuid():N}@example.com", "hashed-password", "Test", "User");
        db.Users.Add(user);

        const string plaintext = "http-layer-verification-token";
        db.EmailVerificationTokens.Add(EmailVerificationToken.Create(user.Id, plaintext, DateTimeOffset.UtcNow.AddHours(24)));
        await db.SaveChangesAsync();

        return plaintext;
    }

    /// <summary>Proves the token is read from the JSON body — the real fix, not just "not 404".</summary>
    [Fact]
    public async Task VerifyEmail_TokenInRequestBody_VerifiesTheAccount()
    {
        var token = await SeedVerifiableUserAsync();

        using var response = await _client.PostAsJsonAsync("api/auth/verify-email", new { token });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// The old, vulnerable shape — token as a query parameter, no JSON body. Binding now requires a
    /// body, so a valid token supplied only via the query string must no longer verify the account.
    /// </summary>
    [Fact]
    public async Task VerifyEmail_TokenOnlyInQueryString_NoBody_IsRejected()
    {
        var token = await SeedVerifiableUserAsync();

        using var response = await _client.PostAsync($"api/auth/verify-email?token={token}", content: null);

        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// <c>AccountActions</c> allows 10 requests/60s (appsettings.json's <c>RateLimiting:AccountActions</c>).
    /// A bogus token always fails business validation (400), so the 11th request being 429 instead of
    /// 400 proves the limiter — not just "some" limiting exists — is actually attached to this endpoint.
    /// </summary>
    [Fact]
    public async Task VerifyEmail_IsRateLimited_AllowsTenRequests_ThenBlocksEleventhWith429()
    {
        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 11; i++)
        {
            using var response = await _client.PostAsJsonAsync(
                "api/auth/verify-email", new { token = "not-a-real-token" });
            statuses.Add(response.StatusCode);
        }

        Assert.All(statuses.Take(10), status => Assert.Equal(HttpStatusCode.BadRequest, status));
        Assert.Equal(HttpStatusCode.TooManyRequests, statuses[10]);
    }
}
