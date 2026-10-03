using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Catalog.Domain.Categories;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Catalog.Application.Features.Categories.CategoryFilters;

/// <summary>One filter the admin has put on a category's list.</summary>
public sealed record CategoryFilterItemDto(
    string GroupKey,
    string DefaultName,
    string? DisplayNameEn,
    string? DisplayNameAr,
    bool IsVisible,
    int ProductCount);

/// <summary>An option group that can still be added to the list.</summary>
public sealed record AvailableFilterGroupDto(string GroupKey, string DefaultName, int ProductCount);

public sealed record CategoryFilterConfigDto(
    Guid CategoryId,
    bool HasOwnList,
    Guid? InheritedFromCategoryId,
    bool InheritedFromDefault,
    IReadOnlyList<CategoryFilterItemDto> Items,
    IReadOnlyList<AvailableFilterGroupDto> AvailableGroups);

/// <summary>Shared lookup used by both the storefront facets and the admin editor.</summary>
internal static class CategoryFilterResolver
{
    /// <summary>
    /// Finds which category's list applies to <paramref name="categoryId"/>: its own, else the nearest ancestor's,
    /// else the store-wide default; null when nothing has been configured (legacy "show everything").
    /// </summary>
    public static async Task<Guid?> ResolveSourceAsync(
        ICatalogDbContext db,
        Guid? categoryId,
        CancellationToken cancellationToken)
    {
        var configured = (await db.CategoryFacetSettings.AsNoTracking()
                .Select(s => s.CategoryId)
                .Distinct()
                .ToListAsync(cancellationToken))
            .ToHashSet();

        if (configured.Count == 0)
        {
            return null;
        }

        var visited = new HashSet<Guid>();
        var current = categoryId;
        while (current is { } id && visited.Add(id))
        {
            if (configured.Contains(id))
            {
                return id;
            }

            current = await db.Categories.AsNoTracking()
                .Where(c => c.Id == id)
                .Select(c => c.ParentId)
                .FirstOrDefaultAsync(cancellationToken);
        }

        return configured.Contains(CategoryFacetSetting.DefaultCategoryId) ? CategoryFacetSetting.DefaultCategoryId : null;
    }
}

public sealed record GetCategoryFilterConfigQuery(Guid CategoryId) : IRequest<Result<CategoryFilterConfigDto>>;

internal sealed class GetCategoryFilterConfigQueryHandler(ICatalogDbContext db)
    : IRequestHandler<GetCategoryFilterConfigQuery, Result<CategoryFilterConfigDto>>
{
    public async Task<Result<CategoryFilterConfigDto>> Handle(
        GetCategoryFilterConfigQuery request,
        CancellationToken cancellationToken)
    {
        var isDefault = request.CategoryId == CategoryFacetSetting.DefaultCategoryId;

        var ownRows = await db.CategoryFacetSettings.AsNoTracking()
            .Where(s => s.CategoryId == request.CategoryId)
            .OrderBy(s => s.SortOrder)
            .ToListAsync(cancellationToken);

        var hasOwnList = ownRows.Count > 0;
        Guid? sourceId = request.CategoryId;
        var inheritedFromDefault = false;
        Guid? inheritedFrom = null;
        var rows = ownRows;

        if (!hasOwnList)
        {
            sourceId = await CategoryFilterResolver.ResolveSourceAsync(
                db, isDefault ? null : request.CategoryId, cancellationToken);

            if (sourceId is { } source)
            {
                rows = await db.CategoryFacetSettings.AsNoTracking()
                    .Where(s => s.CategoryId == source)
                    .OrderBy(s => s.SortOrder)
                    .ToListAsync(cancellationToken);

                inheritedFromDefault = source == CategoryFacetSetting.DefaultCategoryId;
                inheritedFrom = inheritedFromDefault ? null : source;
            }
        }

        // Option groups present on the products of this category (or on every product, for the default list),
        // so the admin sees which filters are actually relevant here.
        var productScope = db.Products.AsNoTracking().AsQueryable();
        if (!isDefault)
        {
            var categoryId = request.CategoryId;
            productScope = productScope.Where(p =>
                p.CategoryId == categoryId || p.AdditionalCategories.Any(pc => pc.CategoryId == categoryId));
        }

        var usage = await db.ProductOptionGroups.AsNoTracking()
            .Where(g => productScope.Any(p => p.Id == g.ProductId))
            .GroupBy(g => g.Key)
            .Select(g => new { Key = g.Key, Name = g.Max(x => x.DisplayName)!, Products = g.Select(x => x.ProductId).Distinct().Count() })
            .ToListAsync(cancellationToken);

        var allGroups = await db.ProductOptionGroups.AsNoTracking()
            .GroupBy(g => g.Key)
            .Select(g => new { Key = g.Key, Name = g.Max(x => x.DisplayName)! })
            .ToListAsync(cancellationToken);

        var usageByKey = usage.ToDictionary(u => u.Key, u => u);
        var nameByKey = allGroups.ToDictionary(g => g.Key, g => g.Name);

        var items = rows
            .Select(r => new CategoryFilterItemDto(
                r.GroupKey,
                nameByKey.GetValueOrDefault(r.GroupKey, r.GroupKey),
                r.DisplayNameEn,
                r.DisplayNameAr,
                r.IsVisible,
                usageByKey.TryGetValue(r.GroupKey, out var u) ? u.Products : 0))
            .ToList();

        var listed = items.Select(i => i.GroupKey).ToHashSet();
        var available = allGroups
            .Where(g => !listed.Contains(g.Key))
            .Select(g => new AvailableFilterGroupDto(g.Key, g.Name, usageByKey.TryGetValue(g.Key, out var u) ? u.Products : 0))
            .OrderByDescending(g => g.ProductCount)
            .ThenBy(g => g.DefaultName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return Result.Success(new CategoryFilterConfigDto(
            request.CategoryId, hasOwnList, inheritedFrom, inheritedFromDefault, items, available));
    }
}

public sealed record CategoryFilterInput(string GroupKey, string? DisplayNameEn, string? DisplayNameAr, bool IsVisible);

public sealed record SaveCategoryFilterConfigCommand(Guid CategoryId, IReadOnlyList<CategoryFilterInput> Items)
    : IRequest<Result>;

internal sealed class SaveCategoryFilterConfigCommandHandler(ICatalogDbContext db)
    : IRequestHandler<SaveCategoryFilterConfigCommand, Result>
{
    public async Task<Result> Handle(SaveCategoryFilterConfigCommand request, CancellationToken cancellationToken)
    {
        var existing = await db.CategoryFacetSettings
            .Where(s => s.CategoryId == request.CategoryId)
            .ToListAsync(cancellationToken);
        db.CategoryFacetSettings.RemoveRange(existing);

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var order = 0;
        foreach (var item in request.Items)
        {
            if (string.IsNullOrWhiteSpace(item.GroupKey) || !seen.Add(item.GroupKey.Trim()))
            {
                continue;
            }

            db.CategoryFacetSettings.Add(CategoryFacetSetting.Create(
                request.CategoryId, item.GroupKey, item.DisplayNameEn, item.DisplayNameAr, order++, item.IsVisible));
        }

        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

/// <summary>Removes a category's own list so it goes back to inheriting.</summary>
public sealed record ResetCategoryFilterConfigCommand(Guid CategoryId) : IRequest<Result>;

internal sealed class ResetCategoryFilterConfigCommandHandler(ICatalogDbContext db)
    : IRequestHandler<ResetCategoryFilterConfigCommand, Result>
{
    public async Task<Result> Handle(ResetCategoryFilterConfigCommand request, CancellationToken cancellationToken)
    {
        var existing = await db.CategoryFacetSettings
            .Where(s => s.CategoryId == request.CategoryId)
            .ToListAsync(cancellationToken);
        db.CategoryFacetSettings.RemoveRange(existing);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
