using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Catalog.Application.Contracts;
using HAMBOX.Modules.Catalog.Application.Errors;
using HAMBOX.Modules.Catalog.Application.Services;
using HAMBOX.Modules.Catalog.Domain.Products;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Catalog.Application.Features.Products.Images.SetPrimaryProductImage;

public sealed record SetPrimaryProductImageCommand(Guid ProductId, Guid ImageId) : IRequest<Result<ProductImageDto>>;

internal sealed class SetPrimaryProductImageCommandHandler(
    ICatalogDbContext dbContext) : IRequestHandler<SetPrimaryProductImageCommand, Result<ProductImageDto>>
{
    public async Task<Result<ProductImageDto>> Handle(
        SetPrimaryProductImageCommand request,
        CancellationToken cancellationToken)
    {
        if (dbContext is not DbContext context)
        {
            return Result.Failure<ProductImageDto>(CatalogErrors.ProductNotFound);
        }

        var product = await context.Set<Product>()
            .Include(entry => entry.Images)
            .FirstOrDefaultAsync(entry => entry.Id == request.ProductId, cancellationToken);

        if (product is null)
        {
            return Result.Failure<ProductImageDto>(CatalogErrors.ProductNotFound);
        }

        var image = product.Images.FirstOrDefault(entry => entry.Id == request.ImageId);

        if (image is null)
        {
            return Result.Failure<ProductImageDto>(CatalogErrors.ProductImageNotFound);
        }

        product.SetPrimaryImage(request.ImageId);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(CatalogMapper.ToProductImageDto(image));
    }
}
