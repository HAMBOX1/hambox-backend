using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Catalog.Application.Errors;
using HAMBOX.Modules.Catalog.Domain.Categories;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Catalog.Application.Features.Categories;

internal static class CategoryParentValidator
{
    public static async Task<Result?> ValidateParentAsync(
        ICatalogDbContext db,
        Guid categoryId,
        Guid? parentId,
        CancellationToken cancellationToken)
    {
        if (parentId is null)
        {
            return null;
        }

        if (parentId == categoryId)
        {
            return Result.Failure(CatalogErrors.CategoryCycle);
        }

        var parentExists = await db.Categories.AnyAsync(c => c.Id == parentId, cancellationToken);
        if (!parentExists)
        {
            return Result.Failure(CatalogErrors.CategoryNotFound);
        }

        var cursor = parentId;
        while (cursor is not null)
        {
            var parent = await db.Categories.AsNoTracking()
                .Where(c => c.Id == cursor)
                .Select(c => new { c.Id, c.ParentId })
                .FirstOrDefaultAsync(cancellationToken);

            if (parent is null)
            {
                break;
            }

            if (parent.ParentId == categoryId)
            {
                return Result.Failure(CatalogErrors.CategoryCycle);
            }

            cursor = parent.ParentId;
        }

        return null;
    }
}

public sealed record RestoreCategoryCommand(Guid Id) : IRequest<Result>;

internal sealed class RestoreCategoryCommandHandler : IRequestHandler<RestoreCategoryCommand, Result>
{
    private readonly ICatalogDbContext _dbContext;

    public RestoreCategoryCommandHandler(ICatalogDbContext dbContext) => _dbContext = dbContext;

    public async Task<Result> Handle(RestoreCategoryCommand request, CancellationToken cancellationToken)
    {
        var category = await _dbContext.Categories
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken);

        if (category is null)
        {
            return Result.Failure(CatalogErrors.CategoryNotFound);
        }

        try
        {
            category.Restore();
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure(new HAMBOX.SharedKernel.Errors.Error("Categories.InvalidRestore", ex.Message));
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
