using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Catalog.Application.Contracts;
using HAMBOX.Modules.Catalog.Application.Errors;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Threading;
using System.Threading.Tasks;

namespace HAMBOX.Modules.Catalog.Application.Features.Collections.GetCollectionById;

internal sealed class GetCollectionByIdQueryHandler : IRequestHandler<GetCollectionByIdQuery, Result<CollectionDto>>
{
    private readonly ICatalogDbContext _dbContext;

    public GetCollectionByIdQueryHandler(ICatalogDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<CollectionDto>> Handle(GetCollectionByIdQuery request, CancellationToken cancellationToken)
    {
        var collection = await _dbContext.ProductCollections
            .AsNoTracking()
            .Where(c => c.Id == request.Id)
            .Select(c => new CollectionDto(c.Id, c.Name, c.Description, c.Color, c.Icon, c.ParentId, c.SortOrder, c.IsSystem))
            .FirstOrDefaultAsync(cancellationToken);

        if (collection is null)
        {
            return Result.Failure<CollectionDto>(CatalogErrors.CollectionNotFound);
        }

        return Result.Success(collection);
    }
}
