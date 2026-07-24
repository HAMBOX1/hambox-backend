using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Catalog.Application.Contracts;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Catalog.Application.Features.Collections.GetCollections;

internal sealed class GetCollectionsQueryHandler : IRequestHandler<GetCollectionsQuery, Result<PagedResult<CollectionDto>>>
{
    private readonly ICatalogDbContext _dbContext;

    public GetCollectionsQueryHandler(ICatalogDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<PagedResult<CollectionDto>>> Handle(GetCollectionsQuery request, CancellationToken cancellationToken)
    {
        var query = _dbContext.ProductCollections.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            query = query.Where(c =>
                c.Name.Contains(request.SearchTerm) ||
                (c.Description != null && c.Description.Contains(request.SearchTerm)));
        }

        int totalCount = await query.CountAsync(cancellationToken);

        var collections = await query
            .OrderBy(c => c.ParentId)
            .ThenBy(c => c.SortOrder)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(c => new CollectionDto(c.Id, c.Name, c.Description, c.Color, c.Icon, c.ParentId, c.SortOrder, c.IsSystem))
            .ToListAsync(cancellationToken);

        return Result.Success(new PagedResult<CollectionDto>(collections, request.PageNumber, request.PageSize, totalCount));
    }
}
