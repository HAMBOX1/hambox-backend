using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Catalog.Domain.Collections;
using HAMBOX.SharedKernel.Results;
using MediatR;
using System.Threading;
using System.Threading.Tasks;

namespace HAMBOX.Modules.Catalog.Application.Features.Collections.CreateCollection;

internal sealed class CreateCollectionCommandHandler : IRequestHandler<CreateCollectionCommand, Result<Guid>>
{
    private readonly ICatalogDbContext _dbContext;

    public CreateCollectionCommandHandler(ICatalogDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<Guid>> Handle(CreateCollectionCommand request, CancellationToken cancellationToken)
    {
        var parentError = await CollectionParentValidator.ValidateParentAsync(
            _dbContext,
            Guid.Empty,
            request.ParentId,
            cancellationToken);

        if (parentError is not null)
        {
            return Result.Failure<Guid>(parentError.Error);
        }

        var collection = ProductCollection.Create(
            request.Name,
            request.Description,
            request.Color,
            request.Icon,
            request.ParentId,
            request.SortOrder);

        _dbContext.ProductCollections.Add(collection);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(collection.Id);
    }
}
