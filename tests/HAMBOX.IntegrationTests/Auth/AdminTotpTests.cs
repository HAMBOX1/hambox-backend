using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using HAMBOX.IntegrationTests.RateLimiting;
using HAMBOX.Modules.Identity.Application.Authentication;
using HAMBOX.Modules.Identity.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace HAMBOX.IntegrationTests.Auth;

/// <summary>RFC 6238 conformance of the TOTP primitive (published SHA-1 test vectors, 6-digit truncation).</summary>
public sealed class TotpAuthenticatorTests
{
    // ASCII "12345678901234567890" as base-32 — the RFC 6238 appendix B shared secret.
    private const string Rfc6238Secret = "GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ";

    [Theory]
    [InlineData(59L, "287082")]
    [InlineData(1111111109L, "081804")]
    [InlineData(1111111111L, "050471")]
    [InlineData(1234567890L, "005924")]
    [InlineData(2000000000L, "279037")]
    [InlineData(20000000000L, "353130")]
    public void ComputeCode_MatchesTheRfcVectors(long unixSeconds, string expected)
    {
        var step = TotpAuthenticator.TimeStep(DateTimeOffset.FromUnixTimeSeconds(unixSeconds));

        Assert.Equal(expected, TotpAuthenticator.ComputeCode(Rfc6238Secret, step));
    }

    [Fact]
    public void Verify_AcceptsTheCurrentAndAdjacentSteps_ButNotFurtherAway()
    {
        var now = DateTimeOffset.FromUnixTimeSeconds(1111111111L);
        var step = TotpAuthenticator.TimeStep(now);

        Assert.Equal(step, TotpAuthenticator.Verify(Rfc6238Secret, TotpAuthenticator.ComputeCode(Rfc6238Secret, step), now, 0));
        Assert.Equal(step - 1, TotpAuthenticator.Verify(Rfc6238Secret, TotpAuthenticator.ComputeCode(Rfc6238Secret, step - 1), now, 0));
        Assert.Equal(step + 1, TotpAuthenticator.Verify(Rfc6238Secret, TotpAuthenticator.ComputeCode(Rfc6238Secret, step + 1), now, 0));
        Assert.Null(TotpAuthenticator.Verify(Rfc6238Secret, TotpAuthenticator.ComputeCode(Rfc6238Secret, step + 3), now, 0));
    }

    [Fact]
    public void Verify_RefusesAStepThatWasAlreadyUsed()
    {
        var now = DateTimeOffset.FromUnixTimeSeconds(1111111111L);
        var step = TotpAuthenticator.TimeStep(now);
        var code = TotpAuthenticator.ComputeCode(Rfc6238Secret, step);

        Assert.Null(TotpAuthenticator.Verify(Rfc6238Secret, code, now, lastUsedStep: step));
    }

    [Theory]
    [InlineData("")]
    [InlineData("12345")]
    [InlineData("1234567")]
    [InlineData("abcdef")]
    public void Verify_RejectsMalformedCodes(string code) =>
        Assert.Null(TotpAuthenticator.Verify(Rfc6238Secret, code, DateTimeOffset.UtcNow, 0));

    [Fact]
    public void Base32_RoundTrips()
    {
        var bytes = new byte[] { 1, 2, 3, 250, 251, 252, 0, 255, 17, 34, 51, 68, 85, 102, 119, 136, 153, 170, 187, 204 };

        Assert.Equal(bytes, TotpAuthenticator.Base32Decode(TotpAuthenticator.Base32Encode(bytes)));
    }

    [Fact]
    public void GeneratedSecret_IsValidBase32_AndRecoveryCodesLookRight()
    {
        var secret = TotpAuthenticator.GenerateSecret();

        Assert.Equal(32, secret.Length);
        Assert.Equal(20, TotpAuthenticator.Base32Decode(secret).Length);
        Assert.Matches("^[A-HJ-NP-Z2-9]{5}-[A-HJ-NP-Z2-9]{5}$", TotpAuthenticator.GenerateRecoveryCode());
    }

    [Fact]
    public void OtpAuthUri_CarriesIssuerAccountAndProfile()
    {
        var uri = TotpAuthenticator.BuildOtpAuthUri("HAMBOX Admin", "owner@example.com", Rfc6238Secret);

        Assert.StartsWith("otpauth://totp/", uri);
        Assert.Contains("secret=" + Rfc6238Secret, uri);
        Assert.Contains("issuer=HAMBOX%20Admin", uri);
        Assert.Contains("digits=6", uri);
        Assert.Contains("period=30", uri);
    }
}

/// <summary>
/// End to end through the real host: enrol an authenticator, sign in with it, spend recovery codes, remove it —
/// plus the Production second-factor floor and its operator-owned bypass (audit R7/R8).
/// </summary>
[Collection(HamboxApiFactoryCollection.Name)]
public sealed class AdminTotpFlowTests : IAsyncLifetime
{
    private const string RefreshCookieName = "hambox_rt";

    private readonly HamboxRateLimitWebApplicationFactory _factory = new();
    private string _email = null!;
    private Guid _userId;

    public async Task InitializeAsync()
    {
        await _factory.InitializeIdentitySchemaAsync();
        _email = $"admin-totp-{Guid.NewGuid():N}@example.com";
        _userId = await AdminUserSeeder.SeedAsync(_factory.Services, _email);
        // Outside Production an admin with the e-mailed code switched off signs in on the password alone —
        // that is how the tests obtain the session they enrol from.
        await PlatformSettingsTestHelpers.SetAdminOtpEnabledAsync(_factory.Services, false);
    }

    public async Task DisposeAsync()
    {
        await _factory.DropDatabaseAsync();
        await _factory.DisposeAsync();
    }

    private static HttpClient NewClient(WebApplicationFactory<Program> factory)
    {
        var client = factory.CreateDefaultClient(new Uri("https://localhost"));
        client.DefaultRequestHeaders.Add("User-Agent", "HAMBOX-IntegrationTests/1.0");
        return client;
    }

    private static bool HasRefreshCookie(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Set-Cookie", out var cookies)
        && cookies.Any(c => c.StartsWith(RefreshCookieName + "=", StringComparison.OrdinalIgnoreCase));

    private async Task<JsonElement> AdminLoginAsync(HttpClient client)
    {
        using var response = await client.PostAsJsonAsync(
            "api/auth/admin/login", new { Email = _email, Password = AdminUserSeeder.Password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();
    }

    private static HttpRequestMessage Bearer(HttpMethod method, string path, string accessToken, object? body = null)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return request;
    }

    /// <summary>Signs in (password only, OTP disabled), starts enrolment and confirms it. Returns the secret and recovery codes.</summary>
    private async Task<(string Secret, string[] RecoveryCodes)> EnrolAsync(HttpClient client)
    {
        var login = await AdminLoginAsync(client);
        var access = login.GetProperty("token").GetProperty("accessToken").GetString()!;

        using var begin = await client.SendAsync(Bearer(HttpMethod.Post, "api/auth/admin/totp/enroll", access));
        Assert.Equal(HttpStatusCode.OK, begin.StatusCode);
        var enrolment = JsonDocument.Parse(await begin.Content.ReadAsStringAsync()).RootElement;
        var secret = enrolment.GetProperty("secret").GetString()!;
        Assert.StartsWith("otpauth://totp/", enrolment.GetProperty("otpAuthUri").GetString());

        var code = TotpAuthenticator.ComputeCode(secret, TotpAuthenticator.TimeStep(DateTimeOffset.UtcNow));
        using var confirm = await client.SendAsync(Bearer(HttpMethod.Post, "api/auth/admin/totp/confirm", access, new { Code = code }));
        Assert.Equal(HttpStatusCode.OK, confirm.StatusCode);
        var recovery = JsonDocument.Parse(await confirm.Content.ReadAsStringAsync()).RootElement
            .GetProperty("recoveryCodes").EnumerateArray().Select(e => e.GetString()!).ToArray();
        Assert.Equal(10, recovery.Length);

        return (secret, recovery);
    }

    private async Task<HttpResponseMessage> VerifyAsync(HttpClient client, string challengeId, string code) =>
        await client.PostAsJsonAsync("api/auth/admin/verify-otp", new { ChallengeId = challengeId, Code = code });

    [Fact]
    public async Task Enrolled_AdminLogin_BecomesATotpChallenge_AndNeverIssuesATokenOnPasswordAlone()
    {
        using var client = NewClient(_factory);
        var (secret, _) = await EnrolAsync(client);

        using var response = await client.PostAsJsonAsync(
            "api/auth/admin/login", new { Email = _email, Password = AdminUserSeeder.Password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(HasRefreshCookie(response));

        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("Totp", body.GetProperty("method").GetString());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("token").ValueKind);

        // A code from the next 30-second step is accepted (clock drift window) and signs in.
        var nextStepCode = TotpAuthenticator.ComputeCode(secret, TotpAuthenticator.TimeStep(DateTimeOffset.UtcNow) + 1);
        using var verified = await VerifyAsync(client, body.GetProperty("challengeId").GetString()!, nextStepCode);
        Assert.Equal(HttpStatusCode.OK, verified.StatusCode);
        Assert.True(HasRefreshCookie(verified));
    }

    [Fact]
    public async Task WrongAuthenticatorCode_IsRejected_AndLocksTheChallengeAfterTheMaximumAttempts()
    {
        using var client = NewClient(_factory);
        await EnrolAsync(client);

        var challenge = await AdminLoginAsync(client);
        var id = challenge.GetProperty("challengeId").GetString()!;

        HttpStatusCode last = default;
        for (var i = 0; i < 6; i++)
        {
            using var attempt = await VerifyAsync(client, id, "000000");
            Assert.False(HasRefreshCookie(attempt));
            last = attempt.StatusCode;
        }

        Assert.Equal(HttpStatusCode.BadRequest, last);
    }

    [Fact]
    public async Task AnAuthenticatorCode_CannotBeReplayed_WithinItsOwnWindow()
    {
        using var client = NewClient(_factory);
        var (secret, _) = await EnrolAsync(client);
        var code = TotpAuthenticator.ComputeCode(secret, TotpAuthenticator.TimeStep(DateTimeOffset.UtcNow) + 1);

        var first = await AdminLoginAsync(client);
        using var accepted = await VerifyAsync(client, first.GetProperty("challengeId").GetString()!, code);
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);

        var second = await AdminLoginAsync(client);
        using var replay = await VerifyAsync(client, second.GetProperty("challengeId").GetString()!, code);
        Assert.NotEqual(HttpStatusCode.OK, replay.StatusCode);
        Assert.False(HasRefreshCookie(replay));
    }

    [Fact]
    public async Task RecoveryCode_SignsIn_Once()
    {
        using var client = NewClient(_factory);
        var (_, recovery) = await EnrolAsync(client);

        var first = await AdminLoginAsync(client);
        using var accepted = await VerifyAsync(client, first.GetProperty("challengeId").GetString()!, recovery[0]);
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        Assert.True(HasRefreshCookie(accepted));

        var second = await AdminLoginAsync(client);
        using var replay = await VerifyAsync(client, second.GetProperty("challengeId").GetString()!, recovery[0]);
        Assert.NotEqual(HttpStatusCode.OK, replay.StatusCode);
        Assert.False(HasRefreshCookie(replay));
    }

    [Fact]
    public async Task Disable_NeedsThePasswordAndASecondFactor_AndThenLoginIsPasswordOnlyAgain()
    {
        using var client = NewClient(_factory);
        var (secret, recovery) = await EnrolAsync(client);

        // Sign in with the authenticator to get a session to act from.
        var challenge = await AdminLoginAsync(client);
        var code = TotpAuthenticator.ComputeCode(secret, TotpAuthenticator.TimeStep(DateTimeOffset.UtcNow) + 1);
        using var verified = await VerifyAsync(client, challenge.GetProperty("challengeId").GetString()!, code);
        var access = JsonDocument.Parse(await verified.Content.ReadAsStringAsync()).RootElement
            .GetProperty("accessToken").GetString()!;

        using var wrongPassword = await client.SendAsync(Bearer(
            HttpMethod.Post, "api/auth/admin/totp/disable", access, new { Password = "NotThePassword1!", Code = recovery[0] }));
        Assert.NotEqual(HttpStatusCode.OK, wrongPassword.StatusCode);

        using var wrongCode = await client.SendAsync(Bearer(
            HttpMethod.Post, "api/auth/admin/totp/disable", access, new { Password = AdminUserSeeder.Password, Code = "000000" }));
        Assert.NotEqual(HttpStatusCode.OK, wrongCode.StatusCode);

        using var disabled = await client.SendAsync(Bearer(
            HttpMethod.Post, "api/auth/admin/totp/disable", access, new { Password = AdminUserSeeder.Password, Code = recovery[1] }));
        Assert.Equal(HttpStatusCode.OK, disabled.StatusCode);

        var after = await AdminLoginAsync(client);
        Assert.NotEqual(JsonValueKind.Null, after.GetProperty("token").ValueKind);
    }

    [Fact]
    public async Task Owner_CanResetAnAuthenticator_ForALostDevice_AndTheAdminIsNotLockedOut()
    {
        using var client = NewClient(_factory);
        var (_, recovery) = await EnrolAsync(client);

        // Signed in with a recovery code (the device is "lost"), the owner clears the authenticator.
        var challenge = await AdminLoginAsync(client);
        using var verified = await VerifyAsync(client, challenge.GetProperty("challengeId").GetString()!, recovery[2]);
        Assert.Equal(HttpStatusCode.OK, verified.StatusCode);
        var access = JsonDocument.Parse(await verified.Content.ReadAsStringAsync()).RootElement
            .GetProperty("accessToken").GetString()!;

        using var reset = await client.SendAsync(Bearer(HttpMethod.Delete, $"api/auth/admin/totp/{_userId}", access));
        Assert.Equal(HttpStatusCode.OK, reset.StatusCode);

        var after = await AdminLoginAsync(client);
        Assert.NotEqual(JsonValueKind.Null, after.GetProperty("token").ValueKind);
    }

    // ─── the Production floor and its bypass ────────────────────────────

    private HttpClient ProductionClient(bool allowBypass)
    {
        var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["AdminSecondFactor:AllowOtpBypassInProduction"] = allowBypass ? "true" : "false",
                }));
            builder.ConfigureTestServices(services =>
                services.AddSingleton<IHostEnvironment>(new FakeHostEnvironment(Environments.Production)));
        });
        return NewClient(factory);
    }

    [Fact]
    public async Task Production_WithOtpSwitchedOffInSettings_StillRefusesAPasswordOnlySignIn()
    {
        using var client = ProductionClient(allowBypass: false);

        using var response = await client.PostAsJsonAsync(
            "api/auth/admin/login", new { Email = _email, Password = AdminUserSeeder.Password });

        Assert.False(HasRefreshCookie(response));
        if (response.StatusCode == HttpStatusCode.OK)
        {
            var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
            Assert.Equal(JsonValueKind.Null, body.GetProperty("token").ValueKind);
        }
    }

    [Fact]
    public async Task Production_WithTheOperatorBypassEnabled_SignsInAndAuditsTheBypass()
    {
        using var client = ProductionClient(allowBypass: true);

        using var response = await client.PostAsJsonAsync(
            "api/auth/admin/login", new { Email = _email, Password = AdminUserSeeder.Password });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(HasRefreshCookie(response));

        using var scope = _factory.Services.CreateScope();
        var identity = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        Assert.True(await identity.AdminOtpAuditLogs.AnyAsync(l =>
            l.UserId == _userId && l.Action == "Bypassed" && l.Details!.Contains("AllowOtpBypassInProduction")));
    }

    [Fact]
    public async Task Production_AnEnrolledAuthenticator_IsRequired_EvenWithTheOperatorBypassEnabled()
    {
        using var enrolClient = NewClient(_factory);
        await EnrolAsync(enrolClient);

        using var client = ProductionClient(allowBypass: true);
        using var response = await client.PostAsJsonAsync(
            "api/auth/admin/login", new { Email = _email, Password = AdminUserSeeder.Password });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(HasRefreshCookie(response));
        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("Totp", body.GetProperty("method").GetString());
    }

    [Fact]
    public async Task WrongPassword_OrUnknownAddress_GetTheSameAnswer_SoAdminAddressesCannotBeProbed()
    {
        using var client = NewClient(_factory);

        using var wrongPassword = await client.PostAsJsonAsync(
            "api/auth/admin/login", new { Email = _email, Password = "definitely-not-it" });
        using var unknown = await client.PostAsJsonAsync(
            "api/auth/admin/login", new { Email = $"nobody-{Guid.NewGuid():N}@example.com", Password = "whatever" });

        Assert.Equal(unknown.StatusCode, wrongPassword.StatusCode);
        Assert.Contains("Identity.InvalidCredentials", await wrongPassword.Content.ReadAsStringAsync());
        Assert.Contains("Identity.InvalidCredentials", await unknown.Content.ReadAsStringAsync());
    }

    private sealed class FakeHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "HAMBOX.IntegrationTests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
