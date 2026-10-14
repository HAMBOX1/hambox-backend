using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using HAMBOX.IntegrationTests.RateLimiting;
using HAMBOX.Modules.Identity.Application.Abstractions;
using HAMBOX.Modules.Identity.Domain.Security;
using HAMBOX.Modules.Identity.Domain.Users;
using HAMBOX.Modules.Identity.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace HAMBOX.IntegrationTests.Security;

/// <summary>Number matching (pure) plus enforcement of the phone blocklist on profile update and login (audit R4).</summary>
[Collection(HamboxApiFactoryCollection.Name)]
public sealed class BlockedPhoneTests : IAsyncLifetime
{
    private const string Password = "TestPassword123!";

    private readonly HamboxRateLimitWebApplicationFactory _factory = new();
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        await _factory.InitializeIdentitySchemaAsync();
        await _factory.DisableMaintenanceModeAsync();
        _client = _factory.CreateDefaultClient(new Uri("https://localhost"));
        _client.DefaultRequestHeaders.Add("User-Agent", "HAMBOX-IntegrationTests/1.0");
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DropDatabaseAsync();
        await _factory.DisposeAsync();
    }

    // ─── matching (no host needed) ──────────────────────────────────────

    [Theory]
    [InlineData("01001234567", "01001234567")]
    [InlineData("01001234567", "+201001234567")]
    [InlineData("+201001234567", "00201001234567")]
    [InlineData("+20 100 123 4567", "0100-123-4567")]
    public void SameLine_InAnyFormat_Matches(string blocked, string candidate) =>
        Assert.True(BlockedPhone.Matches(BlockedPhone.Normalize(blocked), BlockedPhone.Normalize(candidate)));

    [Theory]
    [InlineData("01001234567", "01001234568")]
    [InlineData("01001234567", "+966501234567")]
    [InlineData("01001234567", "")]
    public void DifferentLine_DoesNotMatch(string blocked, string candidate) =>
        Assert.False(BlockedPhone.Matches(BlockedPhone.Normalize(blocked), BlockedPhone.Normalize(candidate)));

    [Fact]
    public void ShortNumbers_AreOnlyBlockedOnExactMatch()
    {
        var pattern = BlockedPhone.Normalize("123456");

        Assert.True(BlockedPhone.Matches(pattern, "123456"));
        Assert.False(BlockedPhone.Matches(pattern, "9123456"));
    }

    // ─── enforcement ────────────────────────────────────────────────────

    private async Task<(string Email, Guid UserId)> SeedUserAsync(string? phone = null)
    {
        using var scope = _factory.Services.CreateScope();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();

        var email = $"phone-{Guid.NewGuid():N}@example.com";
        var user = ApplicationUser.Create(email, hasher.HashPassword(Password), "Phone", "Tester");
        user.ConfirmEmail();
        user.Activate();
        if (phone is not null)
        {
            user.UpdateProfile("Phone", "Tester", phone);
        }

        db.Users.Add(user);
        await db.SaveChangesAsync();
        return (email, user.Id);
    }

    private async Task BlockPhoneAsync(string phone)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        db.BlockedPhones.Add(BlockedPhone.Create(phone, "integration-test"));
        await db.SaveChangesAsync();
        scope.ServiceProvider.GetRequiredService<ISecurityBlocklistService>().InvalidateCache();
    }

    private async Task<string> AccessTokenAsync(string email)
    {
        using var response = await _client.PostAsJsonAsync("api/auth/login", new { Email = email, Password, RememberMe = false });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("accessToken").GetString()!;
    }

    [Fact]
    public async Task UpdateProfile_WithABlockedNumber_IsRejected_AnyFormatting()
    {
        var (email, _) = await SeedUserAsync();
        var token = await AccessTokenAsync(email);
        await BlockPhoneAsync("01001234567");

        using var request = new HttpRequestMessage(HttpMethod.Patch, "api/auth/me")
        {
            Content = JsonContent.Create(new
            {
                FirstName = "Phone",
                LastName = "Tester",
                PhoneNumber = "+20 100 123 4567",
                PreferredLanguage = "en",
                PreferredCurrency = "USD",
            }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("PhoneNumberNotAllowed", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Login_ForAnAccountWhosePhoneIsBlocked_FailsLikeBadCredentials()
    {
        var (email, _) = await SeedUserAsync(phone: "01009998877");
        await BlockPhoneAsync("+201009998877");

        using var response = await _client.PostAsJsonAsync("api/auth/login", new { Email = email, Password, RememberMe = false });

        Assert.False(response.IsSuccessStatusCode, "a blocked phone number must not be able to sign in");
    }

    [Fact]
    public async Task Login_ForAnAccountWithAnUnblockedPhone_StillWorks()
    {
        var (email, _) = await SeedUserAsync(phone: "01005554433");
        await BlockPhoneAsync("01009998877");

        using var response = await _client.PostAsJsonAsync("api/auth/login", new { Email = email, Password, RememberMe = false });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
