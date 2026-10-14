using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using HAMBOX.Application.Security;
using HAMBOX.IntegrationTests.RateLimiting;
using HAMBOX.Modules.Identity.Application.Abstractions;
using HAMBOX.Modules.Identity.Domain.Tokens;
using HAMBOX.Modules.Identity.Domain.Users;
using HAMBOX.Modules.Identity.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace HAMBOX.IntegrationTests.Auth;

/// <summary>
/// Runtime coverage for customer self-service account flows (audit R2): forgot/reset password,
/// profile read/update and change-password, against the real host, real handlers and a real database.
/// </summary>
[Collection(HamboxApiFactoryCollection.Name)]
public sealed class AccountSelfServiceTests : IAsyncLifetime
{
    private const string Password = "TestPassword123!";
    private const string NewPassword = "BrandNewPassw0rd!";

    private readonly HamboxRateLimitWebApplicationFactory _factory = new();
    private HttpClient _client = null!;
    private string _email = null!;
    private Guid _userId;

    public async Task InitializeAsync()
    {
        await _factory.InitializeIdentitySchemaAsync();
        await _factory.DisableMaintenanceModeAsync();
        // Turnstile talks to Cloudflare; the human-verification step itself is covered by its own tests, so
        // here it is stubbed to "passed" and these tests exercise the account logic behind it.
        _client = _factory
            .WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            {
                services.RemoveAll<ITurnstileVerificationService>();
                services.AddSingleton<ITurnstileVerificationService, AlwaysPassesTurnstile>();
            }))
            .CreateDefaultClient(new Uri("https://localhost"));
        _client.DefaultRequestHeaders.Add("User-Agent", "HAMBOX-IntegrationTests/1.0");
        _email = $"self-service-{Guid.NewGuid():N}@example.com";
        _userId = await SeedActiveUserAsync(_email);
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DropDatabaseAsync();
        await _factory.DisposeAsync();
    }

    private sealed class AlwaysPassesTurnstile : ITurnstileVerificationService
    {
        public Task<bool> VerifyAsync(string? token, string? remoteIp, string? expectedAction, CancellationToken cancellationToken) =>
            Task.FromResult(true);
    }

    private async Task<Guid> SeedActiveUserAsync(string email)
    {
        using var scope = _factory.Services.CreateScope();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();

        var user = ApplicationUser.Create(email, hasher.HashPassword(Password), "Test", "User");
        user.ConfirmEmail();
        user.Activate();
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    }

    private async Task<string> SeedResetTokenAsync(TimeSpan lifetime)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();

        var plaintext = $"reset-{Guid.NewGuid():N}";
        db.PasswordResetTokens.Add(PasswordResetToken.Create(_userId, plaintext, DateTimeOffset.UtcNow.Add(lifetime)));
        await db.SaveChangesAsync();
        return plaintext;
    }

    private Task<HttpResponseMessage> LoginAsync(string password) =>
        _client.PostAsJsonAsync("api/auth/login", new { Email = _email, Password = password, RememberMe = false });

    private async Task<string> AccessTokenAsync(string password = Password)
    {
        using var response = await LoginAsync(password);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("accessToken").GetString()!;
    }

    private HttpRequestMessage Authorized(HttpMethod method, string path, string accessToken, object? body = null)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return request;
    }

    private async Task<int> ResetTokenCountAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        return await db.PasswordResetTokens.CountAsync(t => t.UserId == _userId);
    }

    // ─── forgot password ────────────────────────────────────────────────

    [Fact]
    public async Task ForgotPassword_KnownAndUnknownEmail_GetTheSameAnswer_SoAccountsCannotBeEnumerated()
    {
        using var known = await _client.PostAsJsonAsync("api/auth/forgot-password", new { Email = _email, TurnstileToken = "stub" });
        using var unknown = await _client.PostAsJsonAsync(
            "api/auth/forgot-password", new { Email = $"nobody-{Guid.NewGuid():N}@example.com", TurnstileToken = "stub" });

        Assert.Equal(known.StatusCode, unknown.StatusCode);
        Assert.True(known.IsSuccessStatusCode, $"forgot-password answered {known.StatusCode}");
    }

    [Fact]
    public async Task ForgotPassword_KnownEmail_IssuesAResetToken()
    {
        Assert.Equal(0, await ResetTokenCountAsync());

        using var response = await _client.PostAsJsonAsync("api/auth/forgot-password", new { Email = _email, TurnstileToken = "stub" });

        Assert.True(response.IsSuccessStatusCode, $"forgot-password answered {response.StatusCode}");
        Assert.Equal(1, await ResetTokenCountAsync());
    }

    // ─── reset password ─────────────────────────────────────────────────

    [Fact]
    public async Task ResetPassword_WithValidToken_ChangesThePassword_AndTokenIsSingleUse()
    {
        var token = await SeedResetTokenAsync(TimeSpan.FromHours(1));

        using var reset = await _client.PostAsJsonAsync("api/auth/reset-password", new { Token = token, NewPassword });
        Assert.True(reset.IsSuccessStatusCode, $"reset-password answered {reset.StatusCode}");

        using var oldLogin = await LoginAsync(Password);
        Assert.False(oldLogin.IsSuccessStatusCode, "the old password must stop working");

        using var newLogin = await LoginAsync(NewPassword);
        Assert.Equal(HttpStatusCode.OK, newLogin.StatusCode);

        using var replay = await _client.PostAsJsonAsync(
            "api/auth/reset-password", new { Token = token, NewPassword = "AnotherPassw0rd!" });
        Assert.False(replay.IsSuccessStatusCode, "a reset token must not be usable twice");
    }

    [Fact]
    public async Task ResetPassword_WithExpiredToken_IsRejected_AndPasswordIsUnchanged()
    {
        var token = await SeedResetTokenAsync(TimeSpan.FromMilliseconds(300));
        await Task.Delay(TimeSpan.FromMilliseconds(600));

        using var reset = await _client.PostAsJsonAsync("api/auth/reset-password", new { Token = token, NewPassword });
        Assert.False(reset.IsSuccessStatusCode, "an expired token must be rejected");

        using var login = await LoginAsync(Password);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    [Fact]
    public async Task ResetPassword_WithUnknownToken_IsRejected()
    {
        using var reset = await _client.PostAsJsonAsync(
            "api/auth/reset-password", new { Token = "definitely-not-issued", NewPassword });

        Assert.False(reset.IsSuccessStatusCode);
    }

    // ─── profile ────────────────────────────────────────────────────────

    [Fact]
    public async Task Me_RequiresAuthentication()
    {
        using var response = await _client.GetAsync("api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UpdateProfile_PersistsNameAndPreferences_AndMeReturnsThem()
    {
        var token = await AccessTokenAsync();

        using var update = await _client.SendAsync(Authorized(HttpMethod.Patch, "api/auth/me", token, new
        {
            FirstName = "Renamed",
            LastName = "Customer",
            PhoneNumber = "+201001234567",
            PreferredLanguage = "ar",
            PreferredCurrency = "EGP",
        }));
        Assert.True(update.IsSuccessStatusCode, $"PATCH me answered {update.StatusCode}: {await update.Content.ReadAsStringAsync()}");

        using var me = await _client.SendAsync(Authorized(HttpMethod.Get, "api/auth/me", token));
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        var body = await me.Content.ReadAsStringAsync();

        Assert.Contains("Renamed", body);
        Assert.Contains("Customer", body);
    }

    // ─── change password ────────────────────────────────────────────────

    [Fact]
    public async Task ChangePassword_WithWrongCurrentPassword_IsRejected_AndNothingChanges()
    {
        var token = await AccessTokenAsync();

        using var response = await _client.SendAsync(Authorized(HttpMethod.Post, "api/auth/change-password", token, new
        {
            CurrentPassword = "NotMyPassword1!",
            NewPassword,
        }));
        Assert.False(response.IsSuccessStatusCode);

        using var login = await LoginAsync(Password);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    [Fact]
    public async Task ChangePassword_WithCorrectCurrentPassword_SwitchesToTheNewPassword()
    {
        var token = await AccessTokenAsync();

        using var response = await _client.SendAsync(Authorized(HttpMethod.Post, "api/auth/change-password", token, new
        {
            CurrentPassword = Password,
            NewPassword,
        }));
        Assert.True(response.IsSuccessStatusCode, $"change-password answered {response.StatusCode}");

        using var oldLogin = await LoginAsync(Password);
        Assert.False(oldLogin.IsSuccessStatusCode, "the old password must stop working");

        using var newLogin = await LoginAsync(NewPassword);
        Assert.Equal(HttpStatusCode.OK, newLogin.StatusCode);
    }
}
