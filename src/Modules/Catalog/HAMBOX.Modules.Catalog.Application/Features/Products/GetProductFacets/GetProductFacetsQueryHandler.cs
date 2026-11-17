using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Catalog.Application.Contracts;
using HAMBOX.Modules.Catalog.Application.Features.Categories.CategoryFilters;
using HAMBOX.Modules.Catalog.Domain.Categories;
using HAMBOX.Modules.Catalog.Domain.Enums;
using HAMBOX.Modules.Catalog.Domain.Filters;
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

        // Custom filters the admin created: only the active ones, and only where they are meant to appear
        // (a filter limited to some platforms / categories is hidden elsewhere).
        var scopeChain = await CategoryFilterResolver.AncestorChainAsync(db, request.CategoryId, cancellationToken);
        var customFilters = (await db.FilterDefinitions.AsNoTracking()
                .Include(d => d.Values)
                .Where(d => d.IsActive)
                .ToListAsync(cancellationToken))
            .Where(d => d.GetScopeCategoryIds() is { Count: > 0 } scope ? scope.Any(scopeChain.Contains) : true)
            .ToDictionary(d => d.Key, StringComparer.OrdinalIgnoreCase);

        var groupKeys = sourceId is not null
            ? configured.Select(s => s.GroupKey).Where(k => !IsCustomKeyOutOfScope(k, customFilters, scopeChain, db)).ToList()
            : (await db.ProductOptionGroups.AsNoTracking()
                    .Select(g => g.Key)
                    .Distinct()
                    .ToListAsync(cancellationToken))
                .Concat(customFilters.Keys)
                .ToList();
        var settingByKey = configured.ToDictionary(s => s.GroupKey, StringComparer.OrdinalIgnoreCase);

        var facetGroups = new List<ProductFacetGroupDto>();

        foreach (var groupKey in groupKeys)
        {
            // A conditional filter stays hidden until the customer has picked one of the values it depends on.
            if (settingByKey.TryGetValue(groupKey, out var conditional) && !DependencyIsMet(conditional, request.AttributeFilters))
            {
                continue;
            }

            if (customFilters.TryGetValue(groupKey, out var custom))
            {
                var customGroup = await BuildCustomGroupAsync(custom, request, groupKey, settingByKey.GetValueOrDefault(groupKey), cancellationToken);
                if (customGroup is not null)
                {
                    facetGroups.Add(customGroup);
                }

                continue;
            }

            // Exclude this group's own selection so its sibling values stay visible/checkable
            // after one of them is selected (standard faceted-nav behavior).
            var matchingProducts = ProductQueryFilters.ApplyAttributeFilters(
                ProductQueryFilters.ApplyBaseFilters(
                    db.Products.AsNoTracking(), request.SearchTerm, request.CategoryId, ProductStatus.Active, db: db),
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

    /// <summary>A custom filter that is configured in a list but switched off (or scoped elsewhere) is not offered.</summary>
    private static bool IsCustomKeyOutOfScope(
        string key,
        IReadOnlyDictionary<string, FilterDefinition> offered,
        IReadOnlyCollection<Guid> scopeChain,
        ICatalogDbContext db) =>
        !offered.ContainsKey(key) && db.FilterDefinitions.Any(d => d.Key == key);

    private async Task<ProductFacetGroupDto?> BuildCustomGroupAsync(
        FilterDefinition definition,
        GetProductFacetsQuery request,
        string groupKey,
        CategoryFacetSetting? setting,
        CancellationToken cancellationToken)
    {
        var matchingProducts = ProductQueryFilters.ApplyAttributeFilters(
            ProductQueryFilters.ApplyBaseFilters(
                db.Products.AsNoTracking(), request.SearchTerm, request.CategoryId, ProductStatus.Active, db: db),
            db,
            request.AttributeFilters,
            excludeKey: groupKey);

        var counts = await db.ProductFilterValues.AsNoTracking()
            .Where(pf => pf.FilterId == definition.Id && matchingProducts.Select(p => p.Id).Contains(pf.ProductId))
            .GroupBy(pf => pf.FilterValueId)
            .Select(g => new { ValueId = g.Key, Count = g.Select(x => x.ProductId).Distinct().Count() })
            .ToDictionaryAsync(x => x.ValueId, x => x.Count, cancellationToken);

        var selected = request.AttributeFilters is not null
            && request.AttributeFilters.TryGetValue(groupKey, out var chosen)
                ? chosen.Select(c => c.Trim().ToLowerInvariant()).ToHashSet()
                : [];

        // A value with no product in view is left out, except one the customer has already picked (never hide an active choice).
        var options = definition.Values
            .Where(v => v.IsActive || selected.Contains(v.Value))
            .Where(v => counts.GetValueOrDefault(v.Id) > 0 || selected.Contains(v.Value) || definition.ShowWhenEmpty)
            .OrderBy(v => v.SortOrder).ThenBy(v => v.LabelEn, StringComparer.OrdinalIgnoreCase)
            .Select(v => new ProductFacetOptionDto(v.Value, v.LabelEn, counts.GetValueOrDefault(v.Id), v.LabelAr))
            .ToList();

        if (options.Count == 0)
        {
            return null;
        }

        return new ProductFacetGroupDto(
            groupKey,
            setting?.DisplayNameEn ?? definition.NameEn,
            options,
            setting?.DisplayNameAr ?? definition.NameAr,
            definition.IsMultiSelect,
            definition.DefaultExpanded,
            true);
    }

    private static bool DependencyIsMet(
        CategoryFacetSetting setting,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? selected)
    {
        if (string.IsNullOrWhiteSpace(setting.DependsOnGroupKey))
        {
            return true;
        }

        var wanted = setting.GetDependsOnValues();
        if (wanted.Count == 0 || selected is null)
        {
            return wanted.Count == 0;
        }

        foreach (var (key, values) in selected)
        {
            if (string.Equals(key, setting.DependsOnGroupKey, StringComparison.OrdinalIgnoreCase)
                && values.Any(v => wanted.Contains(v, StringComparer.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        return false;
    }
}
