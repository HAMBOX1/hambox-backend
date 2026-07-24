using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Catalog.Application.Errors;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Threading;
using System.Threading.Tasks;

namespace HAMBOX.Modules.Catalog.Application.Features.Products.RemoveProductCollection;

internal sealed class RemoveProductCollectionCommandHandler : IRequestHandler<RemoveProductCollectionCommand, Result>
{
    private readonly ICatalogDbContext _dbContext;

    public RemoveProductCollectionCommandHandler(ICatalogDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result> Handle(RemoveProductCollectionCommand request, CancellationToken cancellationToken)
    {
        var product = await _dbContext.Products
            .Include(p => p.Collections)
            .FirstOrDefaultAsync(p => p.Id == request.ProductId, cancellationToken);

        if (product is null)
        {
            return Result.Failure(CatalogErrors.ProductNotFound);
        }

        var removedItem = product.RemoveFromCollection(request.CollectionId);
        if (removedItem is not null)
        {
            _dbContext.ProductCollectionItems.Remove(removedItem);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return Result.Success();
    }
}
