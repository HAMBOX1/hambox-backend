using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Catalog.Application.Contracts;
using HAMBOX.Modules.Catalog.Application.Errors;
using HAMBOX.Modules.Catalog.Application.Services;
using HAMBOX.Modules.Catalog.Domain.Products;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Catalog.Application.Features.Products.Images.ReorderProductImages;

public sealed record ReorderProductImagesCommand(
    Guid ProductId,
    IReadOnlyList<Guid> OrderedImageIds) : IRequest<Result<IReadOnlyList<ProductImageDto>>>;

internal sealed class ReorderProductImagesCommandHandler(
    ICatalogDbContext dbContext) : IRequestHandler<ReorderProductImagesCommand, Result<IReadOnlyList<ProductImageDto>>>
{
    public async Task<Result<IReadOnlyList<ProductImageDto>>> Handle(
        ReorderProductImagesCommand request,
        CancellationToken cancellationToken)
    {
        if (dbContext is not DbContext context)
        {
            return Result.Failure<IReadOnlyList<ProductImageDto>>(CatalogErrors.ProductNotFound);
        }

        var product = await context.Set<Product>()
            .Include(entry => entry.Images)
            .FirstOrDefaultAsync(entry => entry.Id == request.ProductId, cancellationToken);

        if (product is null)
        {
            return Result.Failure<IReadOnlyList<ProductImageDto>>(CatalogErrors.ProductNotFound);
        }

        try
        {
            product.ReorderImages(request.OrderedImageIds);
        }
        catch (InvalidOperationException)
        {
            return Result.Failure<IReadOnlyList<ProductImageDto>>(CatalogErrors.ProductImageNotFound);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(CatalogMapper.ToProductImageDtos(product.Images));
    }
}
