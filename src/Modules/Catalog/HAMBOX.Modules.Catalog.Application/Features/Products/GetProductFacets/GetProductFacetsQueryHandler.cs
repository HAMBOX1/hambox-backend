using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Catalog.Application.Contracts;
using HAMBOX.Modules.Catalog.Application.Features.Categories.CategoryFilters;
using HAMBOX.Modules.Catalog.Domain.Categories;
using HAMBOX.Modules.Catalog.Domain.Enums;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Catalog.Application.Features.Products.GetProductFacets;

internal sealed class GetProductFacetsQueryHandler(ICatalogDbContext db)
    : IRequestHandler<GetProductFacetsQuery, Result<IReadOnlyList<ProductFacetGroupDto>>>
{
    public async Task<Result<IReadOnlyList<ProductFacetGroupDto>>> Handle(
        GetProductFacetsQuery request, CancellationToken cancellationToken)
    {
        // The admin's filter list for this category (own, else nearest ancestor's, else the store-wide default)
        // decides which filters are offered, in which order and under which name. Nothing configured at all
        // keeps the original behaviour: every option group becomes a filter.
        var sourceId = await CategoryFilterResolver.ResolveSourceAsync(db, request.CategoryId, cancellationToken);
        IReadOnlyList<CategoryFacetSetting> configured = sourceId is { } source
            ? await db.CategoryFacetSettings.AsNoTracking()
                .Where(s => s.CategoryId == source && s.IsVisible)
                .OrderBy(s => s.SortOrder)
                .ToListAsync(cancellationToken)
            : [];

        var groupKeys = sourceId is not null
            ? configured.Select(s => s.GroupKey).ToList()
            : await db.ProductOptionGroups.AsNoTracking()
                .Select(g => g.Key)
                .Distinct()
                .ToListAsync(cancellationToken);
        var settingByKey = configured.ToDictionary(s => s.GroupKey, StringComparer.OrdinalIgnoreCase);

        var facetGroups = new List<ProductFacetGroupDto>();

        foreach (var groupKey in groupKeys)
        {
            // Exclude this group's own selection so its sibling values stay visible/checkable
            // after one of them is selected (standard faceted-nav behavior).
            var matchingProducts = ProductQueryFilters.ApplyAttributeFilters(
                ProductQueryFilters.ApplyBaseFilters(
                    db.Products.AsNoTracking(), request.SearchTerm, request.CategoryId, ProductStatus.Active),
                db,
                request.AttributeFilters,
                excludeKey: groupKey);

            var rows = await db.ProductVariantOptions.AsNoTracking()
                .Join(
                    db.ProductVariants.AsNoTracking().Where(v => v.Status == ProductVariantStatus.Active && v.IsVisible),
                    vo => vo.VariantId,
                    v => v.Id,
                    (vo, v) => new { vo.OptionId, v.ProductId })
                .Where(x => matchingProducts.Select(p => p.Id).Contains(x.ProductId))
                .Join(
                    db.ProductOptions.AsNoTracking(),
                    x => x.OptionId,
                    o => o.Id,
                    (x, o) => new { x.ProductId, o.Value, o.Label, o.OptionGroupId })
                .Join(
                    db.ProductOptionGroups.AsNoTracking().Where(g => g.Key == groupKey),
                    x => x.OptionGroupId,
                    g => g.Id,
                    (x, g) => new { x.ProductId, x.Value, x.Label, g.DisplayName })
                .Distinct()
                .GroupBy(x => x.Value)
                .Select(g => new
                {
                    Value = g.Key,
                    Label = g.Max(x => x.Label)!,
                    DisplayName = g.Max(x => x.DisplayName)!,
                    Count = g.Count(),
                })
                .ToListAsync(cancellationToken);

            if (rows.Count == 0)
            {
                continue;
            }

            var options = rows
                .OrderByDescending(r => r.Count)
                .ThenBy(r => r.Label, StringComparer.OrdinalIgnoreCase)
                .Select(r => new ProductFacetOptionDto(r.Value, r.Label, r.Count))
                .ToList();

            settingByKey.TryGetValue(groupKey, out var setting);
            facetGroups.Add(new ProductFacetGroupDto(
                groupKey,
                setting?.DisplayNameEn ?? rows[0].DisplayName,
                options,
                setting?.DisplayNameAr));
        }

        // A configured list keeps the admin's order; the legacy "everything" list stays alphabetical.
        return Result.Success<IReadOnlyList<ProductFacetGroupDto>>(
            sourceId is not null
                ? facetGroups
                : facetGroups.OrderBy(g => g.DisplayName, StringComparer.OrdinalIgnoreCase).ToList());
    }
}
