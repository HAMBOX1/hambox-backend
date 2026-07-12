using HAMBOX.Modules.Themes.Application.Abstractions;
using HAMBOX.Modules.Themes.Application.Services;
using HAMBOX.Modules.Themes.Domain.Themes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HAMBOX.Modules.Themes.Infrastructure.Persistence;

public static class ThemeDataSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ThemesDbContext>();

        if (await db.StoreThemes.IgnoreQueryFilters().AnyAsync())
        {
            return;
        }

        var dark = StoreTheme.Create("HAMBOX Default Dark", "hambox-default-dark", "Default marketplace dark theme", ThemeBaseMode.Dark, isDefault: true);
        var darkVersion = dark.CreateDraftVersion(ThemeMapper.DefaultDarkTokens(), "Seeded default dark theme");
        dark.PublishVersion(darkVersion.Id);
        dark.UpsertAsset(ThemeAssetType.Favicon, "/favicon.ico");

        var light = StoreTheme.Create("HAMBOX Default Light", "hambox-default-light", "Default marketplace light theme", ThemeBaseMode.Light);
        var lightVersion = light.CreateDraftVersion(ThemeMapper.DefaultLightTokens(), "Seeded default light theme");
        light.PublishVersion(lightVersion.Id);

        var gold = StoreTheme.Create("Gold Membership", "gold-membership", "Premium gold theme for membership plans", ThemeBaseMode.Dark);
        var goldTokens = ThemeMapper.DefaultDarkTokens();
        goldTokens[ThemeSemanticTokens.Primary] = "#f59e0b";
        goldTokens[ThemeSemanticTokens.Accent] = "#fbbf24";
        var goldVersion = gold.CreateDraftVersion(goldTokens, "Gold membership theme");
        gold.PublishVersion(goldVersion.Id);
        gold.AddAssignment(ThemeAssignmentType.Membership, "gold", priority: 10);

        db.StoreThemes.AddRange(dark, light, gold);
        await db.SaveChangesAsync();
    }
}
