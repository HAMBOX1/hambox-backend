using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Catalog.Application.Errors;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Catalog.Application.Features.Collections;

internal static class CollectionParentValidator
{
    public static async Task<Result?> ValidateParentAsync(
        ICatalogDbContext db,
        Guid collectionId,
        Guid? parentId,
        CancellationToken cancellationToken)
    {
        if (parentId is null)
        {
            return null;
        }

        if (parentId == collectionId)
        {
            return Result.Failure(CatalogErrors.CollectionCycle);
        }

        var parentExists = await db.ProductCollections.AnyAsync(c => c.Id == parentId, cancellationToken);
        if (!parentExists)
        {
            return Result.Failure(CatalogErrors.CollectionNotFound);
        }

        var cursor = parentId;
        while (cursor is not null)
        {
            var parent = await db.ProductCollections.AsNoTracking()
                .Where(c => c.Id == cursor)
                .Select(c => new { c.Id, c.ParentId })
                .FirstOrDefaultAsync(cancellationToken);

            if (parent is null)
            {
                break;
            }

            if (parent.ParentId == collectionId)
            {
                return Result.Failure(CatalogErrors.CollectionCycle);
            }

            cursor = parent.ParentId;
        }

        return null;
    }
}

public sealed record RestoreCollectionCommand(Guid Id) : IRequest<Result>;

internal sealed class RestoreCollectionCommandHandler : IRequestHandler<RestoreCollectionCommand, Result>
{
    private readonly ICatalogDbContext _dbContext;

    public RestoreCollectionCommandHandler(ICatalogDbContext dbContext) => _dbContext = dbContext;

    public async Task<Result> Handle(RestoreCollectionCommand request, CancellationToken cancellationToken)
    {
        var collection = await _dbContext.ProductCollections
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken);

        if (collection is null)
        {
            return Result.Failure(CatalogErrors.CollectionNotFound);
        }

        try
        {
            collection.Restore();
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure(new HAMBOX.SharedKernel.Errors.Error("Collections.InvalidRestore", ex.Message));
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
