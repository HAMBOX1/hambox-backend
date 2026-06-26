using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Catalog.Application.Errors;
using HAMBOX.Modules.Catalog.Domain.Products;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Catalog.Application.Features.Products.Images.DeleteProductImage;

public sealed record DeleteProductImageCommand(Guid ProductId, Guid ImageId) : IRequest<Result>;

internal sealed class DeleteProductImageCommandHandler(
    ICatalogDbContext dbContext,
    IFileStorage fileStorage) : IRequestHandler<DeleteProductImageCommand, Result>
{
    public async Task<Result> Handle(DeleteProductImageCommand request, CancellationToken cancellationToken)
    {
        if (dbContext is not DbContext context)
        {
            return Result.Failure(CatalogErrors.ProductNotFound);
        }

        var product = await context.Set<Product>()
            .Include(entry => entry.Images)
            .FirstOrDefaultAsync(entry => entry.Id == request.ProductId, cancellationToken);

        if (product is null)
        {
            return Result.Failure(CatalogErrors.ProductNotFound);
        }

        var image = product.Images.FirstOrDefault(entry => entry.Id == request.ImageId);

        if (image is null)
        {
            return Result.Failure(CatalogErrors.ProductImageNotFound);
        }

        var storageKey = image.StorageKey;
        var wasPrimary = image.IsPrimary;

        product.RemoveImage(request.ImageId);

        if (wasPrimary && product.Images.Count > 0)
        {
            var nextPrimary = product.Images.OrderBy(entry => entry.DisplayOrder).First();
            product.SetPrimaryImage(nextPrimary.Id);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(storageKey))
        {
            await fileStorage.DeleteAsync(storageKey, cancellationToken);
        }

        return Result.Success();
    }
}
