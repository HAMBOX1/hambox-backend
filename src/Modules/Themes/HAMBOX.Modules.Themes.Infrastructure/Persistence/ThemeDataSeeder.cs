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

        if (!await db.StoreThemes.IgnoreQueryFilters().AnyAsync())
        {
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

        if (!await db.StoreThemes.IgnoreQueryFilters().AnyAsync(t => t.IsTemplate))
        {
            db.StoreThemes.AddRange(BuildTemplates());
            await db.SaveChangesAsync();
        }
    }

    private static IEnumerable<StoreTheme> BuildTemplates()
    {
        foreach (var (name, description, baseMode, overrides) in TemplateDefinitions())
        {
            var slug = "template-" + name.ToLowerInvariant().Replace(" ", "-");
            var theme = StoreTheme.Create(name, slug, description, baseMode, isTemplate: true);
            var tokens = baseMode == ThemeBaseMode.Dark ? ThemeMapper.DefaultDarkTokens() : ThemeMapper.DefaultLightTokens();
            foreach (var (key, value) in overrides)
            {
                tokens[key] = value;
            }

            theme.CreateDraftVersion(tokens, "Template baseline");
            yield return theme;
        }
    }

    private static IEnumerable<(string Name, string Description, ThemeBaseMode BaseMode, Dictionary<string, string> Overrides)> TemplateDefinitions()
    {
        yield return ("Gaming", "Vivid neon accents for a competitive gaming storefront.", ThemeBaseMode.Dark, new()
        {
            [ThemeSemanticTokens.Primary] = "#7c3aed",
            [ThemeSemanticTokens.Secondary] = "#22d3ee",
            [ThemeSemanticTokens.Accent] = "#22d3ee",
            [ThemeSemanticTokens.Background] = "#0a0a0f",
            [ThemeSemanticTokens.Surface] = "#14141f",
            [ThemeSemanticTokens.Card] = "#1c1c2b",
            [ThemeSemanticTokens.Navbar] = "rgba(10, 10, 15, 0.9)",
            [ThemeSemanticTokens.BorderRadius] = "8px",
        });

        yield return ("Luxury", "Black and gold, understated and premium.", ThemeBaseMode.Dark, new()
        {
            [ThemeSemanticTokens.Primary] = "#d4af37",
            [ThemeSemanticTokens.Secondary] = "#e8c766",
            [ThemeSemanticTokens.Accent] = "#d4af37",
            [ThemeSemanticTokens.Background] = "#0c0c0c",
            [ThemeSemanticTokens.Surface] = "#161514",
            [ThemeSemanticTokens.Card] = "#1e1c19",
            [ThemeSemanticTokens.Navbar] = "rgba(12, 12, 12, 0.92)",
            [ThemeSemanticTokens.BorderRadius] = "4px",
        });

        yield return ("Minimal", "Clean, quiet, mostly-white minimalism.", ThemeBaseMode.Light, new()
        {
            [ThemeSemanticTokens.Primary] = "#111827",
            [ThemeSemanticTokens.Secondary] = "#374151",
            [ThemeSemanticTokens.Accent] = "#111827",
            [ThemeSemanticTokens.Background] = "#ffffff",
            [ThemeSemanticTokens.Surface] = "#ffffff",
            [ThemeSemanticTokens.Card] = "#fafafa",
            [ThemeSemanticTokens.BorderRadius] = "6px",
            [ThemeSemanticTokens.Shadow] = "0 1px 2px rgba(0, 0, 0, 0.04)",
        });

        yield return ("Cyber", "High-contrast cyberpunk pink and cyan.", ThemeBaseMode.Dark, new()
        {
            [ThemeSemanticTokens.Primary] = "#ff2d78",
            [ThemeSemanticTokens.Secondary] = "#00f0ff",
            [ThemeSemanticTokens.Accent] = "#ff2d78",
            [ThemeSemanticTokens.Background] = "#08060c",
            [ThemeSemanticTokens.Surface] = "#120f1a",
            [ThemeSemanticTokens.Card] = "#1a1526",
            [ThemeSemanticTokens.BorderRadius] = "2px",
        });

        yield return ("Steam", "Dark slate blues inspired by digital storefronts.", ThemeBaseMode.Dark, new()
        {
            [ThemeSemanticTokens.Primary] = "#66c0f4",
            [ThemeSemanticTokens.Secondary] = "#1999ff",
            [ThemeSemanticTokens.Accent] = "#66c0f4",
            [ThemeSemanticTokens.Background] = "#171a21",
            [ThemeSemanticTokens.Surface] = "#1b2838",
            [ThemeSemanticTokens.Card] = "#2a3f5a",
        });

        yield return ("PlayStation", "Deep blue with a bright accent.", ThemeBaseMode.Dark, new()
        {
            [ThemeSemanticTokens.Primary] = "#0070d1",
            [ThemeSemanticTokens.Secondary] = "#00a0e9",
            [ThemeSemanticTokens.Accent] = "#00a0e9",
            [ThemeSemanticTokens.Background] = "#0b1220",
            [ThemeSemanticTokens.Surface] = "#101a2c",
            [ThemeSemanticTokens.Card] = "#16223a",
        });

        yield return ("Xbox", "Signature green on near-black.", ThemeBaseMode.Dark, new()
        {
            [ThemeSemanticTokens.Primary] = "#107c10",
            [ThemeSemanticTokens.Secondary] = "#5ea50a",
            [ThemeSemanticTokens.Accent] = "#5ea50a",
            [ThemeSemanticTokens.Background] = "#0e0e0e",
            [ThemeSemanticTokens.Surface] = "#161616",
            [ThemeSemanticTokens.Card] = "#1f1f1f",
        });

        yield return ("Discord", "Indigo brand palette on charcoal surfaces.", ThemeBaseMode.Dark, new()
        {
            [ThemeSemanticTokens.Primary] = "#5865f2",
            [ThemeSemanticTokens.Secondary] = "#eb459e",
            [ThemeSemanticTokens.Accent] = "#5865f2",
            [ThemeSemanticTokens.Background] = "#1e1f22",
            [ThemeSemanticTokens.Surface] = "#2b2d31",
            [ThemeSemanticTokens.Card] = "#313338",
        });

        yield return ("Modern Commerce", "Confident blue, built for a general marketplace.", ThemeBaseMode.Light, new()
        {
            [ThemeSemanticTokens.Primary] = "#2563eb",
            [ThemeSemanticTokens.Secondary] = "#7c3aed",
            [ThemeSemanticTokens.Accent] = "#2563eb",
            [ThemeSemanticTokens.Background] = "#f8fafc",
            [ThemeSemanticTokens.Surface] = "#ffffff",
            [ThemeSemanticTokens.Card] = "#ffffff",
        });

        yield return ("Black Friday", "Red and black, built for a flash-sale campaign.", ThemeBaseMode.Dark, new()
        {
            [ThemeSemanticTokens.Primary] = "#ef4444",
            [ThemeSemanticTokens.Secondary] = "#f97316",
            [ThemeSemanticTokens.Accent] = "#ef4444",
            [ThemeSemanticTokens.Background] = "#0a0a0a",
            [ThemeSemanticTokens.Surface] = "#161616",
            [ThemeSemanticTokens.Card] = "#1f1f1f",
        });

        yield return ("Christmas", "Festive red and green on a crisp white base.", ThemeBaseMode.Light, new()
        {
            [ThemeSemanticTokens.Primary] = "#b91c1c",
            [ThemeSemanticTokens.Secondary] = "#15803d",
            [ThemeSemanticTokens.Accent] = "#b91c1c",
            [ThemeSemanticTokens.Background] = "#fffaf8",
            [ThemeSemanticTokens.Surface] = "#ffffff",
            [ThemeSemanticTokens.Card] = "#fff5f2",
        });
    }
}
