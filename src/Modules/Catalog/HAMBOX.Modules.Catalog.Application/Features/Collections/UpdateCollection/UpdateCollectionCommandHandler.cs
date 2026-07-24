using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Catalog.Application.Errors;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Threading;
using System.Threading.Tasks;

namespace HAMBOX.Modules.Catalog.Application.Features.Collections.UpdateCollection;

internal sealed class UpdateCollectionCommandHandler : IRequestHandler<UpdateCollectionCommand, Result>
{
    private readonly ICatalogDbContext _dbContext;

    public UpdateCollectionCommandHandler(ICatalogDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result> Handle(UpdateCollectionCommand request, CancellationToken cancellationToken)
    {
        var collection = await _dbContext.ProductCollections
            .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken);

        if (collection is null)
        {
            return Result.Failure(CatalogErrors.CollectionNotFound);
        }

        var parentError = await CollectionParentValidator.ValidateParentAsync(
            _dbContext,
            request.Id,
            request.ParentId,
            cancellationToken);

        if (parentError is not null)
        {
            return parentError;
        }

        collection.Update(
            request.Name,
            request.Description,
            request.Color,
            request.Icon,
            request.ParentId,
            request.SortOrder);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
