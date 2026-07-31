using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Catalog.Application.Contracts;
using HAMBOX.Modules.Catalog.Application.Errors;
using HAMBOX.Modules.Catalog.Application.Services;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Catalog.Application.Features.Categories.Images.UploadCategoryImage;

public sealed record UploadCategoryImageCommand(
    Guid CategoryId,
    Stream Content,
    string FileName,
    string ContentType,
    long FileSizeBytes) : IRequest<Result<CategoryImageDto>>;

internal sealed class UploadCategoryImageCommandHandler(
    ICatalogDbContext dbContext,
    IFileStorage fileStorage) : IRequestHandler<UploadCategoryImageCommand, Result<CategoryImageDto>>
{
    public async Task<Result<CategoryImageDto>> Handle(
        UploadCategoryImageCommand request,
        CancellationToken cancellationToken)
    {
        if (request.FileSizeBytes <= 0 || request.FileSizeBytes > fileStorage.MaxFileSizeBytes)
        {
            return Result.Failure<CategoryImageDto>(CatalogErrors.InvalidCategoryImage);
        }

        if (!fileStorage.IsAllowedContentType(request.ContentType))
        {
            return Result.Failure<CategoryImageDto>(CatalogErrors.InvalidCategoryImage);
        }

        var category = await dbContext.Categories
            .FirstOrDefaultAsync(c => c.Id == request.CategoryId, cancellationToken);

        if (category is null)
        {
            return Result.Failure<CategoryImageDto>(CatalogErrors.CategoryNotFound);
        }

        var previousStorageKey = category.ImageStorageKey;

        StoredFileResult storedFile;

        try
        {
            storedFile = await fileStorage.SaveAsync(
                request.Content,
                request.FileName,
                request.ContentType,
                $"categories/{request.CategoryId}",
                cancellationToken);
        }
        catch (InvalidOperationException)
        {
            return Result.Failure<CategoryImageDto>(CatalogErrors.InvalidCategoryImage);
        }

        category.SetImage(storedFile.PublicUrl, storedFile.StorageKey);
        await CategoryImageResolution.ApplyAndPropagateAsync(dbContext, category, cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(previousStorageKey))
        {
            await fileStorage.DeleteAsync(previousStorageKey, cancellationToken);
        }

        return Result.Success(new CategoryImageDto(storedFile.PublicUrl));
    }
}
