using System.Net;
using System.Net.Http.Json;
using HAMBOX.IntegrationTests.RateLimiting;
using HAMBOX.Modules.Identity.Presentation.Endpoints;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace HAMBOX.IntegrationTests.Auth;

/// <summary>
/// Proves two P0 fixes against the real host (real <c>AdminLoginCommandHandler</c>,
/// <c>ResendAdminOtpCommandHandler</c>, <c>LoggingEmailService</c>):
/// 1. Production never skips admin OTP, even when <c>Authentication.AdminOtpEnabled</c> is (still)
///    <see langword="false"/> in Platform Settings — the environment check is the backend-authoritative
///    floor that setting cannot override.
/// 2. OTP delivery failure fails the login closed (no challenge left usable, no token issued) instead of
///    silently "succeeding" by writing the code to the application log.
/// </summary>
[Collection(HamboxApiFactoryCollection.Name)]
public sealed class AdminOtpEnforcementTests : IAsyncLifetime
{
    private const string RefreshCookieName = "hambox_rt";

    private readonly HamboxRateLimitWebApplicationFactory _factory = new();
    private string _adminEmail = null!;

    public async Task InitializeAsync()
    {
        await _factory.InitializeIdentitySchemaAsync();
        _adminEmail = $"admin-otp-test-{Guid.NewGuid():N}@example.com";
        await AdminUserSeeder.SeedAsync(_factory.Services, _adminEmail);
    }

    public async Task DisposeAsync()
    {
        await _factory.DropDatabaseAsync();
        await _factory.DisposeAsync();
    }

    private Task SetAdminOtpEnabledAsync(bool enabled) =>
        PlatformSettingsTestHelpers.SetAdminOtpEnabledAsync(_factory.Services, enabled);

    private Task SetEmailEnabledAsync(bool enabled) =>
        PlatformSettingsTestHelpers.SetEmailEnabledAsync(_factory.Services, enabled);

    private static HttpClient CreateClient(WebApplicationFactory<Program> factory)
    {
        var client = factory.CreateDefaultClient(new Uri("https://localhost"));
        // AdminLoginCommandHandler's LoginHistory recording requires a non-empty User-Agent — a
        // pre-existing, unrelated requirement, same as CookieAuthFlowTests.
        client.DefaultRequestHeaders.Add("User-Agent", "HAMBOX-IntegrationTests/1.0");
        return client;
    }

    private Task<HttpResponseMessage> AdminLoginAsync(HttpClient client) =>
        client.PostAsJsonAsync("api/auth/admin/login", new { Email = _adminEmail, Password = AdminUserSeeder.Password });

    private static bool HasRefreshCookie(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Set-Cookie", out var cookies)
        && cookies.Any(c => c.StartsWith(RefreshCookieName + "=", StringComparison.OrdinalIgnoreCase));

    [Fact]
    public async Task AdminLogin_WithOtpDisabled_OutsideProduction_StillBypassesOtp()
    {
        // Baseline: proves the fix didn't remove the legitimate dev/staging "OTP disabled" capability —
        // only Production should refuse to honor it.
        await SetAdminOtpEnabledAsync(false);
        using var client = CreateClient(_factory);

        using var response = await AdminLoginAsync(client);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<AdminLoginChallengeResponseDto>();
        Assert.NotNull(body!.Token);
        Assert.True(HasRefreshCookie(response));
    }

    [Fact]
    public async Task AdminLogin_WithOtpDisabled_InProduction_NeverIssuesATokenWithoutOtp()
    {
        await SetAdminOtpEnabledAsync(false);

        // Override only the DI-resolved IHostEnvironment that AdminLoginCommandHandler sees — the real
        // ASPNETCORE_ENVIRONMENT stays "Testing" for Program.cs's own startup branches (migrations/demo
        // seeding), so this stays a fast, isolated test, not a full Production boot.
        using var productionFactory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
                services.AddSingleton<IHostEnvironment>(new FakeHostEnvironment(Environments.Production))));
        using var client = CreateClient(productionFactory);

        using var response = await AdminLoginAsync(client);

        // Either outcome is acceptable proof that OTP was not bypassed: a challenge awaiting OTP
        // verification (200, Token null), or the challenge failing to send in this sandboxed
        // environment (400 AdminOtpDeliveryFailed, covered on its own below) — what must never happen
        // is an immediately-authenticated response.
        Assert.False(HasRefreshCookie(response));
        if (response.StatusCode == HttpStatusCode.OK)
        {
            var body = await response.Content.ReadFromJsonAsync<AdminLoginChallengeResponseDto>();
            Assert.Null(body!.Token);
        }
    }

    [Fact]
    public async Task AdminLogin_WhenOtpDeliveryFails_RejectsLogin_WithNoTokenIssued()
    {
        // AdminOtpEnabled stays at its true default; disabling email delivery routes the OTP send to
        // LoggingEmailService, which (outside Development) now fails closed instead of logging the code.
        await SetEmailEnabledAsync(false);
        using var client = CreateClient(_factory);

        using var response = await AdminLoginAsync(client);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.False(HasRefreshCookie(response));
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Identity.AdminOtpDeliveryFailed", body);
    }

    [Fact]
    public async Task ResendAdminOtp_WhenDeliveryFails_RejectsResend_WithNoTokenIssued()
    {
        // The initial login's own OTP send must succeed first, to get a real, still-active challenge
        // to resend against. The default Email settings point at a real SMTP host unreachable from
        // this sandbox, so routing through LoggingEmailService (Email disabled) via a Development-
        // overridden client is the only way to get a successful send without a real mail server —
        // same scratch database underneath either way.
        await SetEmailEnabledAsync(false);
        using var devFactory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
                services.AddSingleton<IHostEnvironment>(new FakeHostEnvironment(Environments.Development))));
        using var devClient = CreateClient(devFactory);
        using var loginResponse = await AdminLoginAsync(devClient);
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var loginBody = await loginResponse.Content.ReadFromJsonAsync<AdminLoginChallengeResponseDto>();
        Assert.NotEqual(Guid.Empty, loginBody!.ChallengeId);

        // Resend through the normal (non-Development) client — delivery fails closed here.
        using var normalClient = CreateClient(_factory);
        using var resendResponse = await normalClient.PostAsJsonAsync(
            "api/auth/admin/resend-otp", new { ChallengeId = loginBody.ChallengeId });

        Assert.Equal(HttpStatusCode.BadRequest, resendResponse.StatusCode);
        var body = await resendResponse.Content.ReadAsStringAsync();
        Assert.Contains("Identity.AdminOtpDeliveryFailed", body);
    }

    private sealed class FakeHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "HAMBOX.IntegrationTests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
