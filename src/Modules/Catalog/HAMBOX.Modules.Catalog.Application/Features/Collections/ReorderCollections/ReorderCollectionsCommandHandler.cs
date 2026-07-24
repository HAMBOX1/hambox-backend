using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Catalog.Application.Errors;
using HAMBOX.Modules.Catalog.Application.Features.Collections;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Catalog.Application.Features.Collections.ReorderCollections;

internal sealed class ReorderCollectionsCommandHandler(ICatalogDbContext dbContext)
    : IRequestHandler<ReorderCollectionsCommand, Result>
{
    public async Task<Result> Handle(ReorderCollectionsCommand request, CancellationToken cancellationToken)
    {
        if (request.Entries.Count == 0)
        {
            return Result.Success();
        }

        var ids = request.Entries.Select(e => e.Id).ToList();
        var collections = await dbContext.ProductCollections
            .Where(c => ids.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, cancellationToken);

        foreach (var entry in request.Entries)
        {
            if (!collections.TryGetValue(entry.Id, out var collection))
            {
                return Result.Failure(CatalogErrors.CollectionNotFound);
            }

            if (entry.ParentId != collection.ParentId)
            {
                var parentError = await CollectionParentValidator.ValidateParentAsync(
                    dbContext,
                    entry.Id,
                    entry.ParentId,
                    cancellationToken);

                if (parentError is not null)
                {
                    return parentError;
                }

                collection.SetParent(entry.ParentId);
            }

            collection.SetSortOrder(entry.SortOrder);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
