using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Catalog.Application.Contracts;
using HAMBOX.Modules.Catalog.Domain.Enums;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Catalog.Application.Features.Products.GetProducts;

internal sealed class GetProductsQueryHandler : IRequestHandler<GetProductsQuery, Result<PagedResult<ProductDto>>>
{
    private readonly ICatalogDbContext _dbContext;

    public GetProductsQueryHandler(ICatalogDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<PagedResult<ProductDto>>> Handle(GetProductsQuery request, CancellationToken cancellationToken)
    {
        var query = _dbContext.Products.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            query = query.Where(p => p.NameAr.Contains(request.SearchTerm) ||
                                     p.NameEn.Contains(request.SearchTerm) ||
                                     p.DescriptionAr.Contains(request.SearchTerm) ||
                                     p.DescriptionEn.Contains(request.SearchTerm));
        }

        if (request.Status.HasValue)
        {
            query = query.Where(p => p.Status == request.Status.Value);
        }

        if (request.CategoryId.HasValue)
        {
            query = query.Where(p => p.CategoryId == request.CategoryId.Value);
        }

        query = ApplySort(query, request.SortBy);

        var totalCount = await query.CountAsync(cancellationToken);

        var products = await query
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(p => new ProductDto(
                p.Id,
                p.NameAr,
                p.NameEn,
                p.DescriptionAr,
                p.DescriptionEn,
                p.Price,
                p.Status.ToString(),
                p.CategoryId,
                _dbContext.Categories.FirstOrDefault(c => c.Id == p.CategoryId)!.NameEn,
                p.Images.FirstOrDefault(i => i.IsPrimary)!.Url
                    ?? p.Images.OrderBy(i => i.DisplayOrder).Select(i => i.Url).FirstOrDefault(),
                null,
                p.CreatedOnUtc))
            .ToListAsync(cancellationToken);

        return Result.Success(new PagedResult<ProductDto>(products, request.PageNumber, request.PageSize, totalCount));
    }

    private static IQueryable<Domain.Products.Product> ApplySort(
        IQueryable<Domain.Products.Product> query,
        ProductSortBy? sortBy)
    {
        return sortBy switch
        {
            ProductSortBy.PriceAsc => query.OrderBy(p => p.Price).ThenByDescending(p => p.CreatedOnUtc),
            ProductSortBy.PriceDesc => query.OrderByDescending(p => p.Price).ThenByDescending(p => p.CreatedOnUtc),
            ProductSortBy.Newest => query.OrderByDescending(p => p.CreatedOnUtc),
            _ => query.OrderByDescending(p => p.CreatedOnUtc),
        };
    }
}
