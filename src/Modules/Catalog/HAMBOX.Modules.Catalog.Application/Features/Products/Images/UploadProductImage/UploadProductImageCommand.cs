using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Catalog.Application.Contracts;
using HAMBOX.Modules.Catalog.Application.Errors;
using HAMBOX.Modules.Catalog.Application.Services;
using HAMBOX.Modules.Catalog.Domain.Images;
using HAMBOX.Modules.Catalog.Domain.Products;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Catalog.Application.Features.Products.Images.UploadProductImage;

public sealed record UploadProductImageCommand(
    Guid ProductId,
    Stream Content,
    string FileName,
    string ContentType,
    long FileSizeBytes) : IRequest<Result<ProductImageDto>>;

internal sealed class UploadProductImageCommandHandler(
    ICatalogDbContext dbContext,
    IFileStorage fileStorage) : IRequestHandler<UploadProductImageCommand, Result<ProductImageDto>>
{
    public async Task<Result<ProductImageDto>> Handle(
        UploadProductImageCommand request,
        CancellationToken cancellationToken)
    {
        if (request.FileSizeBytes <= 0 || request.FileSizeBytes > fileStorage.MaxFileSizeBytes)
        {
            return Result.Failure<ProductImageDto>(CatalogErrors.InvalidProductImage);
        }

        if (!fileStorage.IsAllowedContentType(request.ContentType))
        {
            return Result.Failure<ProductImageDto>(CatalogErrors.InvalidProductImage);
        }

        if (dbContext is not DbContext context)
        {
            return Result.Failure<ProductImageDto>(CatalogErrors.ProductNotFound);
        }

        var product = await context.Set<Product>()
            .AsNoTracking()
            .Include(entry => entry.Images)
            .FirstOrDefaultAsync(entry => entry.Id == request.ProductId, cancellationToken);

        if (product is null)
        {
            return Result.Failure<ProductImageDto>(CatalogErrors.ProductNotFound);
        }

        if (product.Images.Count >= CatalogMapper.MaxImagesPerProduct)
        {
            return Result.Failure<ProductImageDto>(CatalogErrors.ProductImageLimitReached);
        }

        StoredFileResult storedFile;

        try
        {
            storedFile = await fileStorage.SaveAsync(
                request.Content,
                request.FileName,
                request.ContentType,
                $"products/{request.ProductId}",
                cancellationToken);
        }
        catch (InvalidOperationException)
        {
            return Result.Failure<ProductImageDto>(CatalogErrors.InvalidProductImage);
        }

        var displayOrder = product.Images.Count == 0
            ? 0
            : product.Images.Max(image => image.DisplayOrder) + 1;

        var isPrimary = product.Images.Count == 0;

        var image = product.AddImage(
            storedFile.PublicUrl,
            storedFile.StorageKey,
            storedFile.FileName,
            storedFile.ContentType,
            storedFile.FileSizeBytes,
            displayOrder,
            isPrimary);

        context.Set<ProductImage>().Add(image);

        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(CatalogMapper.ToProductImageDto(image));
    }
}
