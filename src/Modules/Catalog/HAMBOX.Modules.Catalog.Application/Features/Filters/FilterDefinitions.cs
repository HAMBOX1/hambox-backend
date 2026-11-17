using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Catalog.Domain.Enums;
using HAMBOX.Modules.Catalog.Domain.Filters;
using HAMBOX.SharedKernel.Errors;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Catalog.Application.Features.Filters;

/// <summary>One value of a custom filter, with how many products carry it.</summary>
public sealed record FilterValueDto(
    Guid? Id, string Value, string LabelEn, string? LabelAr, int SortOrder, bool IsActive, int ProductCount = 0);

/// <summary>A custom filter as the admin edits it.</summary>
public sealed record FilterDefinitionDto(
    Guid Id,
    string Key,
    string NameEn,
    string? NameAr,
    bool IsMultiSelect,
    bool IsActive,
    bool ShowWhenEmpty,
    bool DefaultExpanded,
    IReadOnlyList<Guid> ScopeCategoryIds,
    int SortOrder,
    int ProductCount,
    IReadOnlyList<FilterValueDto> Values,
    string? ModifiedBy,
    DateTimeOffset? ModifiedOnUtc);

/// <summary>What the admin sends when creating or updating a filter: its settings and the full list of its values.</summary>
public sealed record SaveFilterDefinitionInput(
    string NameEn,
    string? NameAr,
    bool IsMultiSelect,
    bool IsActive,
    bool ShowWhenEmpty,
    bool DefaultExpanded,
    IReadOnlyList<Guid>? ScopeCategoryIds,
    IReadOnlyList<FilterValueDto> Values,
    string? Key = null);

internal static class FilterErrors
{
    public static readonly Error NotFound = new("Filters.NotFound", "This filter was not found.");
    public static readonly Error NameRequired = new("Filters.NameRequired", "The filter needs a name.");
    public static readonly Error KeyTaken = new("Filters.KeyTaken", "Another filter or option group already uses this key.");
    public static readonly Error ValueNeedsLabel = new("Filters.ValueNeedsLabel", "Every value needs a name.");
    public static readonly Error DuplicateValue = new("Filters.DuplicateValue", "Two values of this filter have the same key.");
}

internal static class FilterMapping
{
    public static FilterDefinitionDto ToDto(FilterDefinition d, IReadOnlyDictionary<Guid, int> valueCounts, int productCount) => new(
        d.Id, d.Key, d.NameEn, d.NameAr, d.IsMultiSelect, d.IsActive, d.ShowWhenEmpty, d.DefaultExpanded,
        d.GetScopeCategoryIds(), d.SortOrder, productCount,
        d.Values.OrderBy(v => v.SortOrder).ThenBy(v => v.LabelEn)
            .Select(v => new FilterValueDto(v.Id, v.Value, v.LabelEn, v.LabelAr, v.SortOrder, v.IsActive, valueCounts.GetValueOrDefault(v.Id)))
            .ToList(),
        d.ModifiedBy, d.ModifiedOnUtc);
}

public sealed record GetFilterDefinitionsQuery : IRequest<Result<IReadOnlyList<FilterDefinitionDto>>>;

internal sealed class GetFilterDefinitionsQueryHandler(ICatalogDbContext db)
    : IRequestHandler<GetFilterDefinitionsQuery, Result<IReadOnlyList<FilterDefinitionDto>>>
{
    public async Task<Result<IReadOnlyList<FilterDefinitionDto>>> Handle(
        GetFilterDefinitionsQuery request, CancellationToken cancellationToken)
    {
        var definitions = await db.FilterDefinitions.AsNoTracking()
            .Include(d => d.Values)
            .OrderBy(d => d.SortOrder).ThenBy(d => d.NameEn)
            .ToListAsync(cancellationToken);

        var counts = await db.ProductFilterValues.AsNoTracking()
            .GroupBy(p => p.FilterValueId)
            .Select(g => new { g.Key, Count = g.Select(x => x.ProductId).Distinct().Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, cancellationToken);
        var productCounts = await db.ProductFilterValues.AsNoTracking()
            .GroupBy(p => p.FilterId)
            .Select(g => new { g.Key, Count = g.Select(x => x.ProductId).Distinct().Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, cancellationToken);

        return Result.Success<IReadOnlyList<FilterDefinitionDto>>(
            definitions.Select(d => FilterMapping.ToDto(d, counts, productCounts.GetValueOrDefault(d.Id))).ToList());
    }
}

/// <summary>Creates a filter (Id null) or updates one, replacing its list of values. Removed values are untagged from products.</summary>
public sealed record SaveFilterDefinitionCommand(Guid? Id, SaveFilterDefinitionInput Input) : IRequest<Result<FilterDefinitionDto>>;

internal sealed class SaveFilterDefinitionCommandHandler(ICatalogDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<SaveFilterDefinitionCommand, Result<FilterDefinitionDto>>
{
    public async Task<Result<FilterDefinitionDto>> Handle(SaveFilterDefinitionCommand request, CancellationToken cancellationToken)
    {
        var input = request.Input;
        if (string.IsNullOrWhiteSpace(input.NameEn))
        {
            return Result.Failure<FilterDefinitionDto>(FilterErrors.NameRequired);
        }

        var values = input.Values ?? [];
        if (values.Any(v => string.IsNullOrWhiteSpace(v.LabelEn)))
        {
            return Result.Failure<FilterDefinitionDto>(FilterErrors.ValueNeedsLabel);
        }

        var normalizedValues = values
            .Select(v => (Dto: v, Slug: FilterDefinition.NormalizeKey(string.IsNullOrWhiteSpace(v.Value) ? v.LabelEn : v.Value)))
            .ToList();
        if (normalizedValues.Any(v => v.Slug.Length == 0) || normalizedValues.Select(v => v.Slug).Distinct().Count() != normalizedValues.Count)
        {
            return Result.Failure<FilterDefinitionDto>(FilterErrors.DuplicateValue);
        }

        FilterDefinition? definition = null;
        if (request.Id is { } id)
        {
            definition = await db.FilterDefinitions.Include(d => d.Values).FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
            if (definition is null)
            {
                return Result.Failure<FilterDefinitionDto>(FilterErrors.NotFound);
            }
        }

        var key = definition?.Key ?? FilterDefinition.NormalizeKey(string.IsNullOrWhiteSpace(input.Key) ? input.NameEn : input.Key);
        if (key.Length == 0)
        {
            return Result.Failure<FilterDefinitionDto>(FilterErrors.NameRequired);
        }

        if (definition is null)
        {
            var taken = await db.FilterDefinitions.AnyAsync(d => d.Key == key, cancellationToken)
                || await db.ProductOptionGroups.AnyAsync(g => g.Key == key, cancellationToken);
            if (taken)
            {
                return Result.Failure<FilterDefinitionDto>(FilterErrors.KeyTaken);
            }

            definition = FilterDefinition.Create(key, input.NameEn, input.NameAr);
            definition.SetSortOrder(await db.FilterDefinitions.CountAsync(cancellationToken));
            db.FilterDefinitions.Add(definition);
        }

        definition.Update(
            input.NameEn, input.NameAr, input.IsMultiSelect, input.IsActive, input.ShowWhenEmpty, input.DefaultExpanded,
            input.ScopeCategoryIds ?? [], currentUser.UserId);

        // Values keep their identity (and their product tags) when sent back with their Id or their key.
        var existing = definition.Values.ToList();
        var kept = new HashSet<Guid>();
        var order = 0;
        foreach (var (dto, slug) in normalizedValues)
        {
            var match = existing.FirstOrDefault(v => dto.Id is { } vid && v.Id == vid)
                ?? existing.FirstOrDefault(v => v.Value == slug);
            if (match is null)
            {
                var added = definition.AddValue(slug, dto.LabelEn, dto.LabelAr, order);
                db.FilterValues.Add(added);
                kept.Add(added.Id);
            }
            else
            {
                match.Update(dto.LabelEn, dto.LabelAr, order, dto.IsActive);
                kept.Add(match.Id);
            }

            order++;
        }

        var removed = existing.Where(v => !kept.Contains(v.Id)).Select(v => v.Id).ToList();
        if (removed.Count > 0)
        {
            db.ProductFilterValues.RemoveRange(await db.ProductFilterValues.Where(p => removed.Contains(p.FilterValueId)).ToListAsync(cancellationToken));
            db.FilterValues.RemoveRange(existing.Where(v => removed.Contains(v.Id)));
        }

        await db.SaveChangesAsync(cancellationToken);

        var counts = await db.ProductFilterValues.AsNoTracking().Where(p => p.FilterId == definition.Id)
            .GroupBy(p => p.FilterValueId)
            .Select(g => new { g.Key, Count = g.Select(x => x.ProductId).Distinct().Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, cancellationToken);
        var productCount = await db.ProductFilterValues.AsNoTracking().Where(p => p.FilterId == definition.Id)
            .Select(p => p.ProductId).Distinct().CountAsync(cancellationToken);
        return Result.Success(FilterMapping.ToDto(definition, counts, productCount));
    }
}

/// <summary>Deletes a filter, its values and the tags on products, and takes it out of every filter list. Products are untouched.</summary>
public sealed record DeleteFilterDefinitionCommand(Guid Id) : IRequest<Result>;

internal sealed class DeleteFilterDefinitionCommandHandler(ICatalogDbContext db) : IRequestHandler<DeleteFilterDefinitionCommand, Result>
{
    public async Task<Result> Handle(DeleteFilterDefinitionCommand request, CancellationToken cancellationToken)
    {
        var definition = await db.FilterDefinitions.Include(d => d.Values).FirstOrDefaultAsync(d => d.Id == request.Id, cancellationToken);
        if (definition is null)
        {
            return Result.Failure(FilterErrors.NotFound);
        }

        db.ProductFilterValues.RemoveRange(await db.ProductFilterValues.Where(p => p.FilterId == definition.Id).ToListAsync(cancellationToken));
        db.FilterValues.RemoveRange(definition.Values);
        db.CategoryFacetSettings.RemoveRange(await db.CategoryFacetSettings.Where(s => s.GroupKey == definition.Key).ToListAsync(cancellationToken));
        db.FilterDefinitions.Remove(definition);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

/// <summary>How a set of values is applied to a product's tags for one filter.</summary>
public enum FilterAssignMode
{
    /// <summary>Add the values and keep what the product already has.</summary>
    Merge = 0,

    /// <summary>The product ends up with exactly these values.</summary>
    Replace = 1,

    /// <summary>Take these values off the product.</summary>
    Remove = 2,
}

public sealed record AssignFilterValuesResultDto(int ProductsChanged, int Added, int Removed, IReadOnlyList<string> UnknownValues);

/// <summary>Tags (or untags) many products with values of one filter. Prices, stock and everything else is left alone.</summary>
public sealed record AssignFilterValuesCommand(
    IReadOnlyList<Guid> ProductIds, string FilterKey, IReadOnlyList<string> Values, FilterAssignMode Mode)
    : IRequest<Result<AssignFilterValuesResultDto>>;

internal sealed class AssignFilterValuesCommandHandler(ICatalogDbContext db)
    : IRequestHandler<AssignFilterValuesCommand, Result<AssignFilterValuesResultDto>>
{
    public const int MaxProducts = 20000;

    public async Task<Result<AssignFilterValuesResultDto>> Handle(AssignFilterValuesCommand request, CancellationToken cancellationToken)
    {
        var key = FilterDefinition.NormalizeKey(request.FilterKey ?? string.Empty);
        var definition = await db.FilterDefinitions.Include(d => d.Values).FirstOrDefaultAsync(d => d.Key == key, cancellationToken);
        if (definition is null)
        {
            return Result.Failure<AssignFilterValuesResultDto>(FilterErrors.NotFound);
        }

        var productIds = request.ProductIds.Where(id => id != Guid.Empty).Distinct().Take(MaxProducts).ToList();
        var wanted = request.Values.Select(FilterDefinition.NormalizeKey).Where(v => v.Length > 0).Distinct().ToList();
        var byKey = definition.Values.ToDictionary(v => v.Value);
        var unknown = wanted.Where(v => !byKey.ContainsKey(v)).ToList();
        var targetValues = wanted.Where(byKey.ContainsKey).Select(v => byKey[v]).ToList();

        // A single-choice filter can only hold one value per product.
        if (!definition.IsMultiSelect && request.Mode != FilterAssignMode.Remove && targetValues.Count > 1)
        {
            targetValues = [targetValues[0]];
        }

        var changed = 0;
        var added = 0;
        var removed = 0;
        foreach (var chunk in productIds.Chunk(500))
        {
            var ids = chunk.ToList();
            var current = await db.ProductFilterValues.Where(p => p.FilterId == definition.Id && ids.Contains(p.ProductId)).ToListAsync(cancellationToken);
            var byProduct = current.ToLookup(p => p.ProductId);

            foreach (var productId in ids)
            {
                var has = byProduct[productId].ToList();
                var hasValueIds = has.Select(h => h.FilterValueId).ToHashSet();
                var touched = false;

                if (request.Mode == FilterAssignMode.Replace)
                {
                    var keep = targetValues.Select(v => v.Id).ToHashSet();
                    foreach (var row in has.Where(h => !keep.Contains(h.FilterValueId)))
                    {
                        db.ProductFilterValues.Remove(row);
                        removed++;
                        touched = true;
                    }
                }

                if (request.Mode == FilterAssignMode.Remove)
                {
                    var drop = targetValues.Select(v => v.Id).ToHashSet();
                    foreach (var row in has.Where(h => drop.Contains(h.FilterValueId)))
                    {
                        db.ProductFilterValues.Remove(row);
                        removed++;
                        touched = true;
                    }
                }
                else
                {
                    foreach (var value in targetValues.Where(v => !hasValueIds.Contains(v.Id)))
                    {
                        db.ProductFilterValues.Add(ProductFilterValue.Create(productId, definition.Id, value.Id));
                        added++;
                        touched = true;
                    }
                }

                if (touched)
                {
                    changed++;
                }
            }

            await db.SaveChangesAsync(cancellationToken);
        }

        return Result.Success(new AssignFilterValuesResultDto(changed, added, removed, unknown));
    }
}

/// <summary>The custom-filter values one product carries, per filter key.</summary>
public sealed record GetProductFilterValuesQuery(Guid ProductId) : IRequest<Result<IReadOnlyDictionary<string, IReadOnlyList<string>>>>;

internal sealed class GetProductFilterValuesQueryHandler(ICatalogDbContext db)
    : IRequestHandler<GetProductFilterValuesQuery, Result<IReadOnlyDictionary<string, IReadOnlyList<string>>>>
{
    public async Task<Result<IReadOnlyDictionary<string, IReadOnlyList<string>>>> Handle(
        GetProductFilterValuesQuery request, CancellationToken cancellationToken)
    {
        var rows = await (
                from pf in db.ProductFilterValues.AsNoTracking()
                join d in db.FilterDefinitions.AsNoTracking() on pf.FilterId equals d.Id
                join v in db.FilterValues.AsNoTracking() on pf.FilterValueId equals v.Id
                where pf.ProductId == request.ProductId
                select new { d.Key, v.Value })
            .ToListAsync(cancellationToken);

        IReadOnlyDictionary<string, IReadOnlyList<string>> map = rows
            .GroupBy(r => r.Key)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<string>)g.Select(r => r.Value).ToList());
        return Result.Success(map);
    }
}

/// <summary>Sets every custom-filter value of one product at once (the product page's Filters card).</summary>
public sealed record SetProductFilterValuesCommand(Guid ProductId, IReadOnlyDictionary<string, IReadOnlyList<string>> Values)
    : IRequest<Result>;

internal sealed class SetProductFilterValuesCommandHandler(ICatalogDbContext db) : IRequestHandler<SetProductFilterValuesCommand, Result>
{
    public async Task<Result> Handle(SetProductFilterValuesCommand request, CancellationToken cancellationToken)
    {
        var definitions = await db.FilterDefinitions.Include(d => d.Values).ToListAsync(cancellationToken);
        var current = await db.ProductFilterValues.Where(p => p.ProductId == request.ProductId).ToListAsync(cancellationToken);

        foreach (var definition in definitions)
        {
            var chosenKeys = request.Values.TryGetValue(definition.Key, out var list)
                ? list.Select(FilterDefinition.NormalizeKey).Distinct().ToList()
                : [];
            var chosen = definition.Values.Where(v => chosenKeys.Contains(v.Value)).ToList();
            if (!definition.IsMultiSelect && chosen.Count > 1)
            {
                chosen = [chosen[0]];
            }

            var mine = current.Where(c => c.FilterId == definition.Id).ToList();
            foreach (var row in mine.Where(m => chosen.All(c => c.Id != m.FilterValueId)))
            {
                db.ProductFilterValues.Remove(row);
            }

            foreach (var value in chosen.Where(c => mine.All(m => m.FilterValueId != c.Id)))
            {
                db.ProductFilterValues.Add(ProductFilterValue.Create(request.ProductId, definition.Id, value.Id));
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

public sealed record PriceRangeDto(decimal? Min, decimal? Max);

/// <summary>
/// The lowest and highest actual selling price among the products in view (category + search + the other chosen filters), so
/// the storefront's price slider always spans real prices. A product's price is the cheapest of its active variants.
/// </summary>
public sealed record GetProductPriceRangeQuery(
    string? SearchTerm,
    Guid? CategoryId,
    IReadOnlyDictionary<string, IReadOnlyList<string>>? AttributeFilters) : IRequest<Result<PriceRangeDto>>;

internal sealed class GetProductPriceRangeQueryHandler(ICatalogDbContext db)
    : IRequestHandler<GetProductPriceRangeQuery, Result<PriceRangeDto>>
{
    public async Task<Result<PriceRangeDto>> Handle(GetProductPriceRangeQuery request, CancellationToken cancellationToken)
    {
        var query = Products.ProductQueryFilters.ApplyAttributeFilters(
            Products.ProductQueryFilters.ApplyBaseFilters(
                db.Products.AsNoTracking(), request.SearchTerm, request.CategoryId, ProductStatus.Active, db: db,
                categoryScope: await Categories.CategoryFilters.CategoryFilterResolver.DescendantScopeAsync(db, request.CategoryId, cancellationToken)),
            db,
            request.AttributeFilters);

        // Variants sell at their own price, else the product's. (Two plain queries: SQL Server cannot aggregate over a
        // value taken from the outer product row inside a correlated subquery.)
        var variantPrices = db.ProductVariants.AsNoTracking()
            .Where(v => !v.IsDeleted && v.Status == ProductVariantStatus.Active && v.IsVisible)
            .Join(query, v => v.ProductId, p => p.Id, (v, p) => v.PriceOverride ?? p.Price);
        var noVariantPrices = query
            .Where(p => !db.ProductVariants.Any(v => v.ProductId == p.Id && !v.IsDeleted && v.Status == ProductVariantStatus.Active && v.IsVisible))
            .Select(p => p.Price);

        var min = new[] { await variantPrices.MinAsync(x => (decimal?)x, cancellationToken), await noVariantPrices.MinAsync(x => (decimal?)x, cancellationToken) }
            .Where(x => x.HasValue).Select(x => x!.Value).ToList();
        var max = new[] { await variantPrices.MaxAsync(x => (decimal?)x, cancellationToken), await noVariantPrices.MaxAsync(x => (decimal?)x, cancellationToken) }
            .Where(x => x.HasValue).Select(x => x!.Value).ToList();

        return Result.Success(min.Count == 0 ? new PriceRangeDto(null, null) : new PriceRangeDto(min.Min(), max.Max()));
    }
}
