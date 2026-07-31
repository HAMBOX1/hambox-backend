using HAMBOX.Modules.Catalog.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Catalog.Application.Features.ImportExport;

/// <summary>
/// Normalizes a category value that may be either its canonical <c>Slug</c> (old templates, hand-
/// typed) or its friendly <c>NameEn</c> (the template's dropdown now shows names, not slugs — see
/// <see cref="CatalogImportTemplateGenerator"/>) back to the canonical slug, before
/// <see cref="CatalogImportMatcher.BuildPlanAsync"/> runs. Collections need no equivalent step —
/// their dedupe key already is the friendly <c>Name</c> (see <see cref="CatalogDedupeKeys.Collection"/>).
/// Runs after <see cref="CatalogImportCorrectionApplier"/> so a "create new category" correction's
/// synthetic row is already in <paramref name="package"/> and resolves correctly.
/// </summary>
public static class CatalogImportLookupResolver
{
    public static async Task<ParsedCatalogPackage> ResolveAsync(
        ParsedCatalogPackage package, ICatalogDbContext db, CancellationToken cancellationToken)
    {
        var existingCategories = await db.Categories.AsNoTracking()
            .Select(c => new { c.Slug, c.NameEn })
            .ToListAsync(cancellationToken);

        var nameToSlug = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var category in existingCategories)
        {
            nameToSlug[category.NameEn] = category.Slug;
        }

        foreach (var category in package.Categories)
        {
            if (!string.IsNullOrWhiteSpace(category.NameEn) && !string.IsNullOrWhiteSpace(category.Slug))
            {
                nameToSlug[category.NameEn] = category.Slug;
            }
        }

        string Resolve(string value) =>
            string.IsNullOrWhiteSpace(value) ? value : nameToSlug.GetValueOrDefault(value, value);

        var categories = package.Categories
            .Select(c => string.IsNullOrWhiteSpace(c.ParentSlug) ? c : c with { ParentSlug = Resolve(c.ParentSlug) })
            .ToList();

        var products = package.Products
            .Select(p => p with
            {
                CategorySlug = Resolve(p.CategorySlug),
                AdditionalCategorySlugs = p.AdditionalCategorySlugs.Select(Resolve).ToList(),
            })
            .ToList();

        return package with { Categories = categories, Products = products };
    }
}
