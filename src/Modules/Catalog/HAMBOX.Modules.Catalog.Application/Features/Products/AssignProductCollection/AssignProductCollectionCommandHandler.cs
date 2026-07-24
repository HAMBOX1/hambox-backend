using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Catalog.Application.Errors;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Threading;
using System.Threading.Tasks;

namespace HAMBOX.Modules.Catalog.Application.Features.Products.AssignProductCollection;

internal sealed class AssignProductCollectionCommandHandler : IRequestHandler<AssignProductCollectionCommand, Result>
{
    private readonly ICatalogDbContext _dbContext;

    public AssignProductCollectionCommandHandler(ICatalogDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result> Handle(AssignProductCollectionCommand request, CancellationToken cancellationToken)
    {
        var product = await _dbContext.Products
            .Include(p => p.Collections)
            .FirstOrDefaultAsync(p => p.Id == request.ProductId, cancellationToken);

        if (product is null)
        {
            return Result.Failure(CatalogErrors.ProductNotFound);
        }

        var collectionExists = await _dbContext.ProductCollections
            .AnyAsync(c => c.Id == request.CollectionId, cancellationToken);
        if (!collectionExists)
        {
            return Result.Failure(CatalogErrors.CollectionNotFound);
        }

        var newItem = product.AddToCollection(request.CollectionId);
        if (newItem is not null)
        {
            _dbContext.ProductCollectionItems.Add(newItem);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return Result.Success();
    }
}
