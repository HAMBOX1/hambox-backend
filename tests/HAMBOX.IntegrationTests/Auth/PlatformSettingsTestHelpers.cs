using System.Text.Json;
using HAMBOX.Application.Abstractions;
using HAMBOX.Application.PlatformSettings;
using HAMBOX.Modules.Identity.Application.Abstractions;
using HAMBOX.Modules.Identity.Application.Authorization;
using HAMBOX.Modules.Identity.Domain.Users;
using HAMBOX.Modules.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HAMBOX.IntegrationTests.Auth;

/// <summary>
/// Seeds an admin (Owner-role) user directly through the real <see cref="IdentityDbContext"/> —
/// shared by every admin-auth integration test that needs one.
/// </summary>
internal static class AdminUserSeeder
{
    public const string Password = "TestPassword123!";

    public static async Task<Guid> SeedAsync(IServiceProvider services, string email)
    {
        using var scope = services.CreateScope();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();

        var user = ApplicationUser.Create(email, hasher.HashPassword(Password), "Test", "Admin");
        user.ConfirmEmail();
        user.Activate();
        db.Users.Add(user);

        var ownerRole = await db.Roles.FirstAsync(r => r.Name == RoleConstants.Owner);
        db.UserRoles.Add(UserRole.Create(user.Id, ownerRole.Id));

        await db.SaveChangesAsync();
        return user.Id;
    }
}

/// <summary>
/// Shared helpers for flipping a single Platform Settings field through the real
/// <see cref="IPlatformSettingsService"/> write path (read current → change one field → save),
/// mirroring how the admin Settings UI does it — used by every auth/session integration test that
/// needs a non-default setting.
/// </summary>
internal static class PlatformSettingsTestHelpers
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static async Task SetAdminOtpEnabledAsync(IServiceProvider services, bool enabled)
    {
        using var scope = services.CreateScope();
        var provider = scope.ServiceProvider.GetRequiredService<IPlatformSettingsProvider>();
        var current = await provider.GetAuthenticationAsync(CancellationToken.None);
        var updated = current with { AdminOtpEnabled = enabled };
        await SaveAuthenticationAsync(scope.ServiceProvider, updated);
    }

    public static async Task SetAdminSessionLifetimeAsync(
        IServiceProvider services, int idleTimeoutMinutes, int maxLifetimeHours)
    {
        using var scope = services.CreateScope();
        var provider = scope.ServiceProvider.GetRequiredService<IPlatformSettingsProvider>();
        var current = await provider.GetAuthenticationAsync(CancellationToken.None);
        var updated = current with
        {
            AdminIdleTimeoutMinutes = idleTimeoutMinutes,
            AdminMaxSessionLifetimeHours = maxLifetimeHours,
        };
        await SaveAuthenticationAsync(scope.ServiceProvider, updated);
    }

    public static async Task SetEmailEnabledAsync(IServiceProvider services, bool enabled)
    {
        using var scope = services.CreateScope();
        var provider = scope.ServiceProvider.GetRequiredService<IPlatformSettingsProvider>();
        var current = await provider.GetEmailAsync(CancellationToken.None);
        var updated = current with { Enabled = enabled };

        var settingsService = scope.ServiceProvider.GetRequiredService<IPlatformSettingsService>();
        await settingsService.UpdateCategoryAsync(
            PlatformSettingsCategoryKeys.Email,
            JsonSerializer.Serialize(updated, JsonOptions),
            actorUserId: null,
            actorDisplayName: "test-harness");
    }

    private static async Task SaveAuthenticationAsync(IServiceProvider scopedServices, AuthenticationSettingsPayload updated)
    {
        var settingsService = scopedServices.GetRequiredService<IPlatformSettingsService>();
        await settingsService.UpdateCategoryAsync(
            PlatformSettingsCategoryKeys.Authentication,
            JsonSerializer.Serialize(updated, JsonOptions),
            actorUserId: null,
            actorDisplayName: "test-harness");
    }
}
