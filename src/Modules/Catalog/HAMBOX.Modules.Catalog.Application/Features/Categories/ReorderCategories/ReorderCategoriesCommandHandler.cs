using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Catalog.Application.Errors;
using HAMBOX.Modules.Catalog.Application.Features.Categories;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Catalog.Application.Features.Categories.ReorderCategories;

internal sealed class ReorderCategoriesCommandHandler(ICatalogDbContext dbContext)
    : IRequestHandler<ReorderCategoriesCommand, Result>
{
    public async Task<Result> Handle(ReorderCategoriesCommand request, CancellationToken cancellationToken)
    {
        if (request.Entries.Count == 0)
        {
            return Result.Success();
        }

        var ids = request.Entries.Select(e => e.Id).ToList();
        var categories = await dbContext.Categories
            .Where(c => ids.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, cancellationToken);

        foreach (var entry in request.Entries)
        {
            if (!categories.TryGetValue(entry.Id, out var category))
            {
                return Result.Failure(CatalogErrors.CategoryNotFound);
            }

            if (entry.ParentId != category.ParentId)
            {
                var parentError = await CategoryParentValidator.ValidateParentAsync(
                    dbContext,
                    entry.Id,
                    entry.ParentId,
                    cancellationToken);

                if (parentError is not null)
                {
                    return parentError;
                }

                category.SetParent(entry.ParentId);
            }

            category.SetSortOrder(entry.SortOrder);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
