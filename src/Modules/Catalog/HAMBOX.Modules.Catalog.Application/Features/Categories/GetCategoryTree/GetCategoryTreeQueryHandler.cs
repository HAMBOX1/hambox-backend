using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Catalog.Application.Contracts;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Catalog.Application.Features.Categories.GetCategoryTree;

internal sealed class GetCategoryTreeQueryHandler(ICatalogDbContext dbContext)
    : IRequestHandler<GetCategoryTreeQuery, Result<IReadOnlyList<CategoryTreeItemDto>>>
{
    public async Task<Result<IReadOnlyList<CategoryTreeItemDto>>> Handle(
        GetCategoryTreeQuery request,
        CancellationToken cancellationToken)
    {
        var categories = await dbContext.Categories
            .AsNoTracking()
            .OrderBy(c => c.ParentId)
            .ThenBy(c => c.SortOrder)
            .Select(c => new { c.Id, c.NameAr, c.NameEn, c.Slug, c.IsActive, c.ParentId, c.SortOrder })
            .ToListAsync(cancellationToken);

        var childrenCounts = await dbContext.Categories
            .AsNoTracking()
            .Where(c => c.ParentId != null)
            .GroupBy(c => c.ParentId!.Value)
            .Select(g => new { ParentId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.ParentId, g => g.Count, cancellationToken);

        var primaryLinks = await dbContext.Products
            .AsNoTracking()
            .Select(p => new { p.CategoryId, ProductId = p.Id })
            .ToListAsync(cancellationToken);

        var additionalLinks = await dbContext.ProductCategories
            .AsNoTracking()
            .Select(pc => new { pc.CategoryId, pc.ProductId })
            .ToListAsync(cancellationToken);

        // A product counts once per category it's assigned to (primary or additional), so
        // browsing "any assigned category" and this admin tree count stay consistent.
        var productCounts = primaryLinks
            .Select(l => (l.CategoryId, l.ProductId))
            .Concat(additionalLinks.Select(l => (l.CategoryId, l.ProductId)))
            .GroupBy(l => l.CategoryId)
            .ToDictionary(g => g.Key, g => g.Select(l => l.ProductId).Distinct().Count());

        var items = categories
            .Select(c => new CategoryTreeItemDto(
                c.Id,
                c.NameAr,
                c.NameEn,
                c.Slug,
                c.IsActive,
                c.ParentId,
                c.SortOrder,
                childrenCounts.GetValueOrDefault(c.Id),
                productCounts.GetValueOrDefault(c.Id)))
            .ToList();

        return Result.Success<IReadOnlyList<CategoryTreeItemDto>>(items);
    }
}
