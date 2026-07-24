using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Catalog.Application.Errors;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Threading;
using System.Threading.Tasks;

namespace HAMBOX.Modules.Catalog.Application.Features.Collections.DeleteCollection;

internal sealed class DeleteCollectionCommandHandler : IRequestHandler<DeleteCollectionCommand, Result>
{
    private readonly ICatalogDbContext _dbContext;

    public DeleteCollectionCommandHandler(ICatalogDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result> Handle(DeleteCollectionCommand request, CancellationToken cancellationToken)
    {
        var collection = await _dbContext.ProductCollections
            .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken);

        if (collection is null)
        {
            return Result.Failure(CatalogErrors.CollectionNotFound);
        }

        if (collection.IsSystem)
        {
            return Result.Failure(CatalogErrors.CollectionIsSystem);
        }

        var hasChildren = await _dbContext.ProductCollections
            .AnyAsync(c => c.ParentId == request.Id, cancellationToken);
        if (hasChildren)
        {
            return Result.Failure(CatalogErrors.CollectionHasChildren);
        }

        var items = await _dbContext.ProductCollectionItems
            .Where(pc => pc.CollectionId == request.Id)
            .ToListAsync(cancellationToken);
        _dbContext.ProductCollectionItems.RemoveRange(items);

        _dbContext.ProductCollections.Remove(collection);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
