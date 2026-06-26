using HAMBOX.Modules.Identity.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace HAMBOX.Modules.Identity.Infrastructure.Extensions;

/// <summary>
/// Extension methods for seeding Identity module development data.
/// </summary>
public static class IdentityDataSeederExtensions
{
    /// <summary>
    /// Seeds development-only Identity data such as the admin test account.
    /// </summary>
    /// <param name="app">The web application.</param>
    /// <returns>A task that represents the asynchronous seed operation.</returns>
    public static async Task SeedIdentityDevelopmentDataAsync(this WebApplication app)
    {
        if (!app.Environment.IsDevelopment())
        {
            return;
        }

        using var scope = app.Services.CreateScope();
        var seeder = scope.ServiceProvider.GetRequiredService<DevAdminDataSeeder>();
        await seeder.SeedAsync();
    }
}
