using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Catalog.Application.Contracts;
using HAMBOX.Modules.Catalog.Application.Errors;
using HAMBOX.Modules.Catalog.Application.Services;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Catalog.Application.Features.Products.GetProductById;

internal sealed class GetProductByIdQueryHandler : IRequestHandler<GetProductByIdQuery, Result<ProductDto>>
{
    private readonly ICatalogDbContext _dbContext;

    public GetProductByIdQueryHandler(ICatalogDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<ProductDto>> Handle(GetProductByIdQuery request, CancellationToken cancellationToken)
    {
        var product = await _dbContext.Products
            .AsNoTracking()
            .Include(entry => entry.Images)
            .FirstOrDefaultAsync(entry => entry.Id == request.Id, cancellationToken);

        if (product is null)
        {
            return Result.Failure<ProductDto>(CatalogErrors.ProductNotFound);
        }

        var categoryName = await _dbContext.Categories
            .AsNoTracking()
            .Where(category => category.Id == product.CategoryId)
            .Select(category => category.NameEn)
            .FirstOrDefaultAsync(cancellationToken) ?? string.Empty;

        return Result.Success(CatalogMapper.ToProductDto(product, categoryName, includeImages: true));
    }
}
