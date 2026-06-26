using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Catalog.Application.Contracts;
using HAMBOX.Modules.Catalog.Application.Errors;
using HAMBOX.Modules.Catalog.Application.Services;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Catalog.Application.Features.Products.Images.GetProductImages;

public sealed record GetProductImagesQuery(Guid ProductId) : IRequest<Result<IReadOnlyList<ProductImageDto>>>;

internal sealed class GetProductImagesQueryHandler(
    ICatalogDbContext dbContext) : IRequestHandler<GetProductImagesQuery, Result<IReadOnlyList<ProductImageDto>>>
{
    public async Task<Result<IReadOnlyList<ProductImageDto>>> Handle(
        GetProductImagesQuery request,
        CancellationToken cancellationToken)
    {
        var productExists = await dbContext.Products
            .AsNoTracking()
            .AnyAsync(product => product.Id == request.ProductId, cancellationToken);

        if (!productExists)
        {
            return Result.Failure<IReadOnlyList<ProductImageDto>>(CatalogErrors.ProductNotFound);
        }

        var images = await dbContext.ProductImages
            .AsNoTracking()
            .Where(image => image.ProductId == request.ProductId)
            .OrderBy(image => image.DisplayOrder)
            .ThenBy(image => image.CreatedOnUtc)
            .ToListAsync(cancellationToken);

        return Result.Success(CatalogMapper.ToProductImageDtos(images));
    }
}
