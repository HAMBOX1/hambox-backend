using HAMBOX.Modules.Catalog.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace HAMBOX.Modules.Catalog.Infrastructure.Extensions;

/// <summary>
/// Extension methods for catalog data seeding.
/// </summary>
public static class CatalogDataSeederExtensions
{
    /// <summary>
    /// Seeds demo catalog data in production when the database is empty.
    /// </summary>
    public static async Task SeedProductionDemoDataAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var seeder = scope.ServiceProvider.GetRequiredService<ProductionDemoDataSeeder>();
        await seeder.SeedAsync();
    }
}
