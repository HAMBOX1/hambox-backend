using HAMBOX.Modules.Catalog.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Catalog.Application.Features.ImportExport;

/// <summary>
/// Normalizes a category value that may be its canonical <c>Slug</c> (old templates, hand-typed), its friendly
/// <c>NameEn</c> (the template's dropdown shows names) or a path of names ("Xbox &gt; Subscriptions &gt; Game Pass", as the
/// client's Products sheet writes it) back to the canonical slug, before <see cref="CatalogImportMatcher.BuildPlanAsync"/>
/// runs. A path segment that does not exist yet becomes a new category row (a child of the segment before it).
/// Collections need no equivalent step — their dedupe key already is the friendly <c>Name</c> (see <see cref="CatalogDedupeKeys.Collection"/>).
/// Runs after <see cref="CatalogImportCorrectionApplier"/> so a "create new category" correction's
/// synthetic row is already in <paramref name="package"/> and resolves correctly.
/// </summary>
public static class CatalogImportLookupResolver
{
    /// <summary>Slug a blank product Category cell falls back to — auto-created as "Uncategorized" by <see cref="CatalogImportMatcher"/> the first time it's needed.</summary>
    public const string DefaultCategorySlug = "uncategorized";

    private const char PathSeparator = '>';

    public static async Task<ParsedCatalogPackage> ResolveAsync(
        ParsedCatalogPackage package, ICatalogDbContext db, CancellationToken cancellationToken)
    {
        var existingCategories = await db.Categories.AsNoTracking()
            .Select(c => new { c.Id, c.Slug, c.NameEn, c.ParentId })
            .ToListAsync(cancellationToken);

        var nameToSlug = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var takenSlugs = new HashSet<string>(existingCategories.Select(c => c.Slug), StringComparer.OrdinalIgnoreCase);
        var childByName = new Dictionary<(string Parent, string Name), string>(PathKeyComparer.Instance);
        var slugById = existingCategories.ToDictionary(c => c.Id, c => c.Slug);
        foreach (var category in existingCategories)
        {
            nameToSlug[category.NameEn] = category.Slug;
            var parent = category.ParentId is { } parentId ? slugById.GetValueOrDefault(parentId, string.Empty) : string.Empty;
            childByName[(parent, category.NameEn)] = category.Slug;
        }

        foreach (var category in package.Categories)
        {
            if (!string.IsNullOrWhiteSpace(category.NameEn) && !string.IsNullOrWhiteSpace(category.Slug))
            {
                nameToSlug[category.NameEn] = category.Slug;
                takenSlugs.Add(category.Slug);
                childByName[(category.ParentSlug ?? string.Empty, category.NameEn)] = category.Slug;
            }
        }

        var created = new List<ParsedCategoryRow>();

        string Resolve(string value) =>
            string.IsNullOrWhiteSpace(value) ? value : nameToSlug.GetValueOrDefault(value, value);

        string ResolvePath(string value)
        {
            if (!value.Contains(PathSeparator))
            {
                return Resolve(value);
            }

            var parent = string.Empty;
            foreach (var segment in value.Split(PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (!childByName.TryGetValue((parent, segment), out var slug))
                {
                    slug = CatalogSlug.Unique(segment, parent.Length == 0 ? null : parent, takenSlugs);
                    created.Add(new ParsedCategoryRow(0, slug, segment, null, parent.Length == 0 ? null : parent, true, 0));
                    childByName[(parent, segment)] = slug;
                    nameToSlug.TryAdd(segment, slug);
                }

                parent = slug;
            }

            return parent;
        }

        var categories = package.Categories
            .Select(c => string.IsNullOrWhiteSpace(c.ParentSlug) ? c : c with { ParentSlug = ResolvePath(c.ParentSlug) })
            .ToList();

        var products = package.Products
            .Select(p => p with
            {
                CategorySlug = string.IsNullOrWhiteSpace(p.CategorySlug) ? DefaultCategorySlug : ResolvePath(p.CategorySlug),
                AdditionalCategorySlugs = p.AdditionalCategorySlugs.Select(ResolvePath).ToList(),
            })
            .ToList();

        return package with { Categories = [.. categories, .. created], Products = products };
    }

    private sealed class PathKeyComparer : IEqualityComparer<(string Parent, string Name)>
    {
        public static readonly PathKeyComparer Instance = new();

        public bool Equals((string Parent, string Name) x, (string Parent, string Name) y) =>
            string.Equals(x.Parent, y.Parent, StringComparison.OrdinalIgnoreCase)
            && string.Equals(x.Name, y.Name, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode((string Parent, string Name) obj) =>
            HashCode.Combine(obj.Parent.ToUpperInvariant(), obj.Name.ToUpperInvariant());
    }
}
