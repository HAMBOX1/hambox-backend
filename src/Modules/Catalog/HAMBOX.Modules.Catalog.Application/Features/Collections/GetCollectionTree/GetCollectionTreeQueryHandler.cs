using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Catalog.Application.Contracts;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Catalog.Application.Features.Collections.GetCollectionTree;

internal sealed class GetCollectionTreeQueryHandler(ICatalogDbContext dbContext)
    : IRequestHandler<GetCollectionTreeQuery, Result<IReadOnlyList<CollectionTreeItemDto>>>
{
    public async Task<Result<IReadOnlyList<CollectionTreeItemDto>>> Handle(
        GetCollectionTreeQuery request,
        CancellationToken cancellationToken)
    {
        var collections = await dbContext.ProductCollections
            .AsNoTracking()
            .OrderBy(c => c.ParentId)
            .ThenBy(c => c.SortOrder)
            .Select(c => new
            {
                c.Id,
                c.Name,
                c.Description,
                c.Color,
                c.Icon,
                c.ParentId,
                c.SortOrder,
                c.IsSystem,
            })
            .ToListAsync(cancellationToken);

        var childrenCounts = await dbContext.ProductCollections
            .AsNoTracking()
            .Where(c => c.ParentId != null)
            .GroupBy(c => c.ParentId!.Value)
            .Select(g => new { ParentId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.ParentId, g => g.Count, cancellationToken);

        var productCounts = await dbContext.ProductCollectionItems
            .AsNoTracking()
            .GroupBy(pc => pc.CollectionId)
            .Select(g => new { CollectionId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.CollectionId, g => g.Count, cancellationToken);

        var items = collections
            .Select(c => new CollectionTreeItemDto(
                c.Id,
                c.Name,
                c.Description,
                c.Color,
                c.Icon,
                c.ParentId,
                c.SortOrder,
                c.IsSystem,
                childrenCounts.GetValueOrDefault(c.Id),
                productCounts.GetValueOrDefault(c.Id)))
            .ToList();

        return Result.Success<IReadOnlyList<CollectionTreeItemDto>>(items);
    }
}
