using System.Text.Json;
using HAMBOX.Application.Abstractions;
using HAMBOX.Application.PlatformSettings;
using HAMBOX.Modules.Content.Application.Contracts.LandingPages;
using HAMBOX.Modules.Content.Application.Services;
using HAMBOX.Modules.Content.Domain.LandingPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HAMBOX.Modules.Content.Infrastructure.Persistence;

/// <summary>
/// Seeds the single "Default" landing page template on first run, mirroring <c>ThemeDataSeeder</c>'s
/// idempotent, Development-only invocation. Every <c>VariantKey</c> here is the canonical first variant
/// per section category for the frontend variant registry (<c>section-variant-registry.ts</c>) — do not
/// rename these strings without updating the registry too.
///
/// Also runs a backfill pass, every startup, that replaces any section's still-placeholder
/// <c>"{}"</c> <c>configJson</c> with real content sourced from the "storefront" Platform Settings
/// category — mirrors Platform Settings' own "merge onto defaults, never clobber a real edit"
/// philosophy: a section's config is only ever touched while it's still the literal seeded
/// placeholder, so an admin's real edits (via the page-builder settings forms) are never overwritten.
/// </summary>
public static class LandingPageDataSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ContentDbContext>();

        if (!await db.LandingPageTemplates.IgnoreQueryFilters().AnyAsync())
        {
            var sections = new List<LandingPageSectionEntry>
            {
                new(Guid.NewGuid(), "Hero", "kinetic-glass", 0, true, "{}"),
                new(Guid.NewGuid(), "Features", "trust-bar", 1, true, "{}"),
                new(Guid.NewGuid(), "Promotions", "promo-banner", 2, true, "{}"),
                new(Guid.NewGuid(), "Categories", "popular-categories", 3, true, "{}"),
                new(Guid.NewGuid(), "Products", "flash-deals", 4, true, "{}"),
                new(Guid.NewGuid(), "Collections", "trending-section", 5, true, "{}"),
                new(Guid.NewGuid(), "Footer", "storefront-footer", 6, true, "{}"),
            };

            var template = LandingPageTemplate.Create("Default", LandingPageSectionsSerializer.Serialize(sections));
            template.Activate();

            db.LandingPageTemplates.Add(template);
            await db.SaveChangesAsync();
        }

        await BackfillPlaceholderConfigAsync(scope.ServiceProvider, db);
    }

    private static async Task BackfillPlaceholderConfigAsync(IServiceProvider services, ContentDbContext db)
    {
        var templates = await db.LandingPageTemplates.ToListAsync();
        if (templates.Count == 0)
        {
            return;
        }

        var settingsProvider = services.GetRequiredService<IPlatformSettingsProvider>();
        var storefront = await settingsProvider.GetStorefrontAsync();
        var dirty = false;

        foreach (var template in templates)
        {
            var (published, publishedChanged) = ReplacePlaceholders(
                LandingPageSectionsSerializer.Deserialize(template.SectionsJson), storefront);

            string? newDraftJson = null;
            if (template.DraftSectionsJson is not null)
            {
                var (draft, draftChanged) = ReplacePlaceholders(
                    LandingPageSectionsSerializer.Deserialize(template.DraftSectionsJson), storefront);
                if (draftChanged)
                {
                    newDraftJson = LandingPageSectionsSerializer.Serialize(draft);
                }
            }

            if (publishedChanged || newDraftJson is not null)
            {
                template.BackfillSectionsJson(LandingPageSectionsSerializer.Serialize(published), newDraftJson);
                dirty = true;
            }
        }

        if (dirty)
        {
            await db.SaveChangesAsync();
        }
    }

    private static (IReadOnlyList<LandingPageSectionEntry> Sections, bool Changed) ReplacePlaceholders(
        IReadOnlyList<LandingPageSectionEntry> sections, StorefrontContentSettingsPayload storefront)
    {
        var changed = false;
        var result = new List<LandingPageSectionEntry>(sections.Count);

        foreach (var section in sections)
        {
            if (section.ConfigJson.Trim() == "{}")
            {
                var replacement = BuildConfigJson(section.Category, section.VariantKey, storefront);
                if (replacement is not null)
                {
                    result.Add(section with { ConfigJson = replacement });
                    changed = true;
                    continue;
                }
            }

            result.Add(section);
        }

        return (result, changed);
    }

    /// <summary>
    /// Maps a seeded section's (category, variantKey) to its real <c>configJson</c>, sourced from the
    /// live "storefront" Platform Settings payload. The per-field shapes below intentionally drop every
    /// `Visible` flag — section-instance visibility already lives on <c>LandingPageSectionEntry.IsVisible</c>,
    /// so it isn't duplicated inside the config blob (mirrors the frontend's `Storefront*Config` types).
    /// Unknown (category, variantKey) pairs (a future variant with no mapping yet) return null — left as "{}".
    /// </summary>
    private static string? BuildConfigJson(string category, string variantKey, StorefrontContentSettingsPayload s) =>
        (category, variantKey) switch
        {
            ("Hero", "kinetic-glass") => JsonSerializer.Serialize(
                new HeroConfig(
                    s.Hero.Title,
                    s.Hero.Subtitle,
                    s.Hero.PrimaryButtonText,
                    s.Hero.PrimaryButtonUrl,
                    s.Hero.SecondaryButtonText,
                    s.Hero.SecondaryButtonUrl,
                    s.Hero.BackgroundImageUrl,
                    s.Hero.OverlayImageUrl,
                    s.Hero.OverlayOpacity,
                    s.Hero.BadgeText,
                    s.Hero.BadgeColor),
                LandingPageSectionsSerializer.Options),

            ("Features", "trust-bar") => JsonSerializer.Serialize(
                new TrustBarConfig(
                    s.TrustBar.Select(t => new TrustItemConfig(t.Id, t.IconUrl, t.Title, t.Description, t.SortOrder)).ToList()),
                LandingPageSectionsSerializer.Options),

            ("Promotions", "promo-banner") => JsonSerializer.Serialize(
                new PromoBannerConfig(
                    s.PromotionalBanner.Title,
                    s.PromotionalBanner.Subtitle,
                    s.PromotionalBanner.ImageUrl,
                    s.PromotionalBanner.ButtonText,
                    s.PromotionalBanner.ButtonUrl,
                    s.PromotionalBanner.CountdownSeconds,
                    s.PromotionalBanner.CountdownEndsAtUtc,
                    s.PromotionalBanner.BackgroundColor),
                LandingPageSectionsSerializer.Options),

            ("Categories", "popular-categories") => JsonSerializer.Serialize(
                new PopularCategoriesConfig(
                    s.PopularCategories.SectionTitle,
                    s.PopularCategories.MaximumCategories,
                    s.PopularCategories.SortOrder),
                LandingPageSectionsSerializer.Options),

            ("Products", "flash-deals") => JsonSerializer.Serialize(
                new FlashDealsConfig(
                    s.FlashDeals.SectionTitle,
                    s.FlashDeals.SectionSubtitle,
                    s.FlashDeals.CountdownEnabled,
                    s.FlashDeals.CountdownDateUtc,
                    s.FlashDeals.MaximumProducts,
                    s.FlashDeals.SortMethod,
                    s.FlashDeals.CountdownSeconds),
                LandingPageSectionsSerializer.Options),

            ("Collections", "trending-section") => JsonSerializer.Serialize(
                new FeaturedCollectionsConfig(
                    s.FeaturedCollections.Title,
                    s.FeaturedCollections.Subtitle,
                    s.FeaturedCollections.MaximumItems,
                    s.FeaturedCollections.DisplayStyle),
                LandingPageSectionsSerializer.Options),

            ("Footer", "storefront-footer") => JsonSerializer.Serialize(
                new FooterConfig(
                    s.Footer.CompanyName,
                    s.Footer.Copyright,
                    s.Footer.SupportEmail,
                    s.Footer.SupportPhone,
                    s.Footer.Address,
                    s.Footer.WorkingHours,
                    s.Footer.FacebookUrl,
                    s.Footer.InstagramUrl,
                    s.Footer.XUrl,
                    s.Footer.DiscordUrl,
                    s.Footer.TelegramUrl,
                    s.Footer.YouTubeUrl,
                    s.Footer.TikTokUrl,
                    s.Footer.WhatsAppUrl),
                LandingPageSectionsSerializer.Options),

            _ => null,
        };

    private sealed record HeroConfig(
        string Title,
        string Subtitle,
        string PrimaryButtonText,
        string PrimaryButtonUrl,
        string SecondaryButtonText,
        string SecondaryButtonUrl,
        string BackgroundImageUrl,
        string? OverlayImageUrl,
        double OverlayOpacity,
        string BadgeText,
        string BadgeColor);

    private sealed record PromoBannerConfig(
        string Title,
        string Subtitle,
        string ImageUrl,
        string ButtonText,
        string ButtonUrl,
        int CountdownSeconds,
        string? CountdownEndsAtUtc,
        string BackgroundColor);

    private sealed record PopularCategoriesConfig(string SectionTitle, int MaximumCategories, string SortOrder);

    private sealed record FlashDealsConfig(
        string SectionTitle,
        string SectionSubtitle,
        bool CountdownEnabled,
        string? CountdownDateUtc,
        int MaximumProducts,
        string SortMethod,
        int CountdownSeconds);

    private sealed record FeaturedCollectionsConfig(string Title, string Subtitle, int MaximumItems, string DisplayStyle);

    private sealed record TrustItemConfig(string Id, string IconUrl, string Title, string Description, int SortOrder);

    private sealed record TrustBarConfig(IReadOnlyList<TrustItemConfig> Items);

    private sealed record FooterConfig(
        string CompanyName,
        string Copyright,
        string SupportEmail,
        string SupportPhone,
        string Address,
        string WorkingHours,
        string? FacebookUrl,
        string? InstagramUrl,
        string? XUrl,
        string? DiscordUrl,
        string? TelegramUrl,
        string? YouTubeUrl,
        string? TikTokUrl,
        string? WhatsAppUrl);
}
