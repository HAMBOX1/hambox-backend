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
            .Include(entry => entry.AdditionalCategories)
            .Include(entry => entry.Collections)
            .FirstOrDefaultAsync(entry => entry.Id == request.Id, cancellationToken);

        if (product is null)
        {
            return Result.Failure<ProductDto>(CatalogErrors.ProductNotFound);
        }

        var category = await _dbContext.Categories
            .AsNoTracking()
            .Where(c => c.Id == product.CategoryId)
            .Select(c => new { c.NameEn, c.NameAr })
            .FirstOrDefaultAsync(cancellationToken);

        return Result.Success(CatalogMapper.ToProductDto(
            product,
            category?.NameEn ?? string.Empty,
            category?.NameAr ?? string.Empty,
            includeImages: true));
    }
}
