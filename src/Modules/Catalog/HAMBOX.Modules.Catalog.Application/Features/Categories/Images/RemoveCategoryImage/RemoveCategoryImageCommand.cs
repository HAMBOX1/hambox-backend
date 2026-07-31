using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Catalog.Application.Errors;
using HAMBOX.Modules.Catalog.Application.Services;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Catalog.Application.Features.Categories.Images.RemoveCategoryImage;

public sealed record RemoveCategoryImageCommand(Guid CategoryId) : IRequest<Result>;

internal sealed class RemoveCategoryImageCommandHandler(
    ICatalogDbContext dbContext,
    IFileStorage fileStorage) : IRequestHandler<RemoveCategoryImageCommand, Result>
{
    public async Task<Result> Handle(RemoveCategoryImageCommand request, CancellationToken cancellationToken)
    {
        var category = await dbContext.Categories
            .FirstOrDefaultAsync(c => c.Id == request.CategoryId, cancellationToken);

        if (category is null)
        {
            return Result.Failure(CatalogErrors.CategoryNotFound);
        }

        if (category.ImageUrl is null)
        {
            return Result.Failure(CatalogErrors.CategoryImageNotFound);
        }

        var storageKey = category.RemoveImage();
        await CategoryImageResolution.ApplyAndPropagateAsync(dbContext, category, cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(storageKey))
        {
            await fileStorage.DeleteAsync(storageKey, cancellationToken);
        }

        return Result.Success();
    }
}
