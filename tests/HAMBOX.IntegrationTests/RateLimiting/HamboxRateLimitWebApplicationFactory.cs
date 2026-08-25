using HAMBOX.Application.PlatformSettings;
using HAMBOX.Modules.Identity.Application.Abstractions;
using HAMBOX.Modules.Identity.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HAMBOX.IntegrationTests.RateLimiting;

/// <summary>
/// Boots the real HAMBOX.API host — real Program.cs, real endpoint mappings, real
/// <c>AddRateLimiter</c> policy registrations — against a scratch LocalDB database, so these tests
/// exercise the production request pipeline rather than a reimplementation of it. The hosting
/// environment is deliberately "Testing" (not "Development"/"Production"): Program.cs only runs
/// migrations and demo-data seeding under those two, so "Testing" boots without ever touching
/// (much less migrating/seeding) the real developer database, keeping this fully isolated.
/// </summary>
public sealed class HamboxRateLimitWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"HamboxRateLimitTests_{Guid.NewGuid():N}";

    private string ConnectionString =>
        $"Server=(localdb)\\MSSQLLocalDB;Database={_databaseName};Trusted_Connection=True;TrustServerCertificate=True;";

    public HamboxRateLimitWebApplicationFactory()
    {
        // Program.cs (top-level statements) calls AddIdentityInfrastructure — which eagerly reads
        // ConnectionStrings:Database and validates JwtSettings:SecretKey — before builder.Build()
        // ever runs. WebApplicationFactory's ConfigureWebHost/ConfigureAppConfiguration callbacks
        // are only wired in at Build() time, too late to affect that earlier code. Real process
        // environment variables, read synchronously by WebApplication.CreateBuilder(args) at the
        // very start, are the only override surface that reaches it in time.
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Testing");
        Environment.SetEnvironmentVariable("ConnectionStrings__Database", ConnectionString);
        // appsettings.Development.json (which carries the real dev secret) is not loaded under
        // "Testing" — supply a harmless, test-only value purely so JwtSettingsValidator's startup
        // check passes. No token issued in these tests is ever presented back to the app, so its
        // value is otherwise irrelevant.
        Environment.SetEnvironmentVariable(
            "JwtSettings__SecretKey", "rate-limit-integration-test-only-secret-key-unused-for-real-auth");
        // Turnstile:SecretKey is likewise fail-fast validated at startup (ValidateOnStart). None of
        // these tests hit a Turnstile-protected endpoint, so Cloudflare's public test credentials are
        // fine here — they exist purely to satisfy the startup check.
        Environment.SetEnvironmentVariable("Turnstile__SiteKey", "1x00000000000000000000AA");
        Environment.SetEnvironmentVariable("Turnstile__SecretKey", "1x0000000000000000000000000000000AA");
    }

    /// <summary>
    /// Applies only the identity schema — the sole schema the maintenance-bypass endpoint touches.
    /// Other modules' DbContexts are left unmigrated on this scratch database; any background
    /// worker that polls them will simply fail its own query and retry later, which does not affect
    /// these tests.
    /// </summary>
    public async Task InitializeIdentitySchemaAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        await db.Database.MigrateAsync();
    }

    /// <summary>
    /// The platform settings default factory sets <c>Maintenance.Enabled</c> to <c>true</c>
    /// ("HAMBOX is launching soon...") for a brand-new, unseeded database — real production
    /// behavior, not a test artifact. Left alone, <c>MaintenanceModeMiddleware</c> would
    /// 503 every <c>/api/v*</c> call on this scratch database before it ever reached an endpoint's
    /// own logic, which is irrelevant to what these rate-limiter tests are checking. Explicitly
    /// disabling it here — through the real <see cref="IPlatformSettingsService"/> write path, not a
    /// hand-crafted row — keeps requests flowing through to each endpoint's actual behavior.
    /// </summary>
    public async Task DisableMaintenanceModeAsync()
    {
        using var scope = Services.CreateScope();
        var settingsService = scope.ServiceProvider.GetRequiredService<IPlatformSettingsService>();
        const string payload = """
            {"enabled":false,"message":"","allowedRoleNames":[],"bypassPassword":null,"hasStoredBypassPassword":false}
            """;
        await settingsService.UpdateCategoryAsync(
            PlatformSettingsCategoryKeys.Maintenance, payload, actorUserId: null, actorDisplayName: "test-harness");
    }

    public async Task DropDatabaseAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        await db.Database.EnsureDeletedAsync();
    }
}
