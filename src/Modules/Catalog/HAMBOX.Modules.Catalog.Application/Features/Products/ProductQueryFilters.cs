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
    public static IQueryable<Product> ApplyBaseFilters(
        IQueryable<Product> query,
        string? searchTerm,
        Guid? categoryId,
        ProductStatus? status)
    {
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            query = query.Where(p => p.NameAr.Contains(searchTerm) ||
                                      p.NameEn.Contains(searchTerm) ||
                                      p.DescriptionAr.Contains(searchTerm) ||
                                      p.DescriptionEn.Contains(searchTerm));
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
