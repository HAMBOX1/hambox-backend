using HAMBOX.Modules.Legal.Domain.Legal;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HAMBOX.Modules.Legal.Infrastructure.Persistence;

/// <summary>
/// Seeds the default set of legal sections (empty drafts) so the admin Legal Center always has
/// something to edit on a fresh database. Idempotent — a no-op once any LegalSection row exists.
/// </summary>
public static class LegalDataSeeder
{
    private static readonly (string Slug, string Title, string? Category, string Icon, bool RequireAcceptance)[] DefaultSections =
    [
        ("terms", "Terms", "Legal", "pi pi-file-check", true),
        ("privacy", "Privacy", "Legal", "pi pi-shield", true),
        ("refund", "Refund", "Legal", "pi pi-replay", true),
        ("delivery", "DigitalDelivery", "Commerce", "pi pi-bolt", true),
        ("cookie", "Cookie", "Legal", "pi pi-info-circle", false),
        ("about", "AboutUs", "General", "pi pi-info-circle", false),
        ("contact", "Contact", "General", "pi pi-envelope", false),
    ];

    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LegalDbContext>();

        if (await db.LegalSections.IgnoreQueryFilters().AnyAsync())
        {
            return;
        }

        foreach (var (slug, title, category, icon, requireAcceptance) in DefaultSections)
        {
            var section = LegalSection.Create(slug);
            section.UpdateMetadata(
                slug,
                category,
                icon,
                sortOrder: 0,
                descriptionEn: null,
                descriptionAr: null,
                seoTitle: null,
                seoDescription: null,
                seoKeywords: null,
                showInFooter: true,
                showInNavigation: true,
                requireAcceptance: requireAcceptance);
            section.CreateDraftVersion(title, null, string.Empty, null, null);
            db.LegalSections.Add(section);
        }

        await db.SaveChangesAsync();
    }
}
