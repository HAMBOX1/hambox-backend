using System.Linq;
using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Catalog.Domain.Enums;
using HAMBOX.Modules.Catalog.Domain.Products;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Catalog.Application.Features.Products;

/// <summary>
/// Shared product filtering used by both the storefront product list and the facet
/// aggregation queries, so the two never drift out of sync.
/// </summary>
internal static class ProductQueryFilters
{
    private static IQueryable<Product> ApplyBasicSearch(IQueryable<Product> query, string searchTerm) =>
        query.Where(p => p.NameAr.Contains(searchTerm) ||
                         p.NameEn.Contains(searchTerm) ||
                         p.DescriptionAr.Contains(searchTerm) ||
                         p.DescriptionEn.Contains(searchTerm));

    /// <summary>
    /// Search that understands how products are really named: every word of the query has to match something on the
    /// product — its name or description, its category, the SKU of a variant, or an option (group name, label or value,
    /// e.g. "Essential", "Deluxe", "1 Month"). "Essential 1" therefore finds the product whose variants are
    /// "Essential" and "1 Month", which a plain name search never could.
    /// </summary>
    private static IQueryable<Product> ApplySearch(IQueryable<Product> query, ICatalogDbContext db, string searchTerm)
    {
        var words = searchTerm
            .Split([' ', '\t', '-', '_'], StringSplitOptions.RemoveEmptyEntries)
            .Select(w => w.Trim())
            .Where(w => w.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(6)
            .ToList();

        foreach (var word in words)
        {
            var term = word;
            query = query.Where(p =>
                p.NameAr.Contains(term) ||
                p.NameEn.Contains(term) ||
                p.DescriptionAr.Contains(term) ||
                p.DescriptionEn.Contains(term) ||
                db.Categories.Any(c => c.Id == p.CategoryId && (c.NameEn.Contains(term) || c.NameAr.Contains(term))) ||
                db.ProductVariants.Any(v => v.ProductId == p.Id && !v.IsDeleted && v.Sku.Contains(term)) ||
                db.ProductOptionGroups.Any(g => g.ProductId == p.Id &&
                    (g.DisplayName.Contains(term) || g.Options.Any(o => o.Label.Contains(term) || o.Value.Contains(term)))));
        }

        return query;
    }

    public static IQueryable<Product> ApplyBaseFilters(
        IQueryable<Product> query,
        string? searchTerm,
        Guid? categoryId,
        ProductStatus? status,
        Guid? collectionId = null,
        bool includePendingMerge = false,
        ICatalogDbContext? db = null)
    {
        // Products parked as a pending merge (see the Merge Products feature) are duplicates
        // awaiting an admin decision — hide them from both the storefront and the default admin
        // catalog list the same way, at this single shared choke point, so neither ever needs its
        // own copy of this rule.
        if (!includePendingMerge)
        {
            query = query.Where(p => p.PendingMergeIntoProductId == null);
        }

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            query = db is null ? ApplyBasicSearch(query, searchTerm) : ApplySearch(query, db, searchTerm);
        }

        if (status.HasValue)
        {
            query = query.Where(p => p.Status == status.Value);
        }

        if (categoryId.HasValue)
        {
            query = query.Where(p =>
                p.CategoryId == categoryId.Value ||
                p.AdditionalCategories.Any(pc => pc.CategoryId == categoryId.Value));
        }

        // Collections are an internal, admin-only organization axis and are never exposed to the
        // storefront — this filter is only ever populated from the admin product list (GetProductsQuery).
        if (collectionId.HasValue)
        {
            query = query.Where(p => p.Collections.Any(pc => pc.CollectionId == collectionId.Value));
        }

        return query;
    }

    /// <summary>
    /// Narrows <paramref name="query"/> to products matching every selected option-group filter
    /// (AND across groups, OR within a group's values). Pass <paramref name="excludeKey"/> to skip
    /// a group's own filter when computing that group's facet counts.
    /// </summary>
    public static IQueryable<Product> ApplyAttributeFilters(
        IQueryable<Product> query,
        ICatalogDbContext db,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? attributeFilters,
        string? excludeKey = null)
    {
        if (attributeFilters is not { Count: > 0 })
        {
            return query;
        }

        foreach (var (key, values) in attributeFilters)
        {
            var normalizedKey = key.Trim().ToLowerInvariant();
            if (normalizedKey == excludeKey)
            {
                continue;
            }

            var normalizedValues = values
                .Select(v => v.Trim().ToLowerInvariant())
                .Where(v => v.Length > 0)
                .Distinct()
                .ToList();

            if (normalizedValues.Count == 0)
            {
                continue;
            }

            var matchingProductIds = MatchingProductIds(db, normalizedKey, normalizedValues);
            query = query.Where(p => matchingProductIds.Contains(p.Id));
        }

        return query;
    }

    public static IQueryable<Guid> MatchingProductIds(
        ICatalogDbContext db,
        string normalizedKey,
        IReadOnlyCollection<string> normalizedValues)
    {
        return db.ProductVariantOptions.AsNoTracking()
            .Join(
                db.ProductVariants.AsNoTracking().Where(v => v.Status == ProductVariantStatus.Active && v.IsVisible),
                vo => vo.VariantId,
                v => v.Id,
                (vo, v) => new { vo.OptionId, v.ProductId })
            .Join(
                db.ProductOptions.AsNoTracking(),
                x => x.OptionId,
                o => o.Id,
                (x, o) => new { x.ProductId, o.Value, o.OptionGroupId })
            .Join(
                db.ProductOptionGroups.AsNoTracking().Where(g => g.Key == normalizedKey),
                x => x.OptionGroupId,
                g => g.Id,
                (x, _) => x)
            .Where(x => normalizedValues.Contains(x.Value))
            .Select(x => x.ProductId)
            .Distinct();
    }
}
