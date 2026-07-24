using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Catalog.Application.Errors;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Catalog.Application.Features.Instructions.UploadProductInstructionsImage;

internal sealed class UploadProductInstructionsImageCommandHandler(
    ICatalogDbContext dbContext,
    IFileStorage fileStorage) : IRequestHandler<UploadProductInstructionsImageCommand, Result<string>>
{
    public async Task<Result<string>> Handle(
        UploadProductInstructionsImageCommand request,
        CancellationToken cancellationToken)
    {
        if (request.FileSizeBytes <= 0 || request.FileSizeBytes > fileStorage.MaxFileSizeBytes)
        {
            return Result.Failure<string>(CatalogErrors.InvalidInstructionsImage);
        }

        if (!fileStorage.IsAllowedContentType(request.ContentType))
        {
            return Result.Failure<string>(CatalogErrors.InvalidInstructionsImage);
        }

        var productExists = await dbContext.Products
            .AsNoTracking()
            .AnyAsync(p => p.Id == request.ProductId, cancellationToken);

        if (!productExists)
        {
            return Result.Failure<string>(CatalogErrors.ProductNotFound);
        }

        StoredFileResult storedFile;

        try
        {
            storedFile = await fileStorage.SaveAsync(
                request.Content,
                request.FileName,
                request.ContentType,
                $"products/{request.ProductId}/instructions",
                cancellationToken);
        }
        catch (InvalidOperationException)
        {
            return Result.Failure<string>(CatalogErrors.InvalidInstructionsImage);
        }

        return Result.Success(storedFile.PublicUrl);
    }
}
