using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Catalog.Application.Contracts;
using HAMBOX.Modules.Catalog.Domain.Analytics;
using HAMBOX.Modules.Catalog.Domain.Enums;
using HAMBOX.Modules.Catalog.Domain.Inventory;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HAMBOX.Modules.Catalog.Application.Features.Products.GetProducts;

internal sealed class GetProductsQueryHandler : IRequestHandler<GetProductsQuery, Result<PagedResult<ProductDto>>>
{
    private readonly ICatalogDbContext _dbContext;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<GetProductsQueryHandler> _logger;

    public GetProductsQueryHandler(
        ICatalogDbContext dbContext,
        ICurrentUserService currentUser,
        ILogger<GetProductsQueryHandler> logger)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _logger = logger;
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
                _dbContext.Categories.FirstOrDefault(c => c.Id == p.CategoryId)!.NameAr,
                p.Images.FirstOrDefault(i => i.IsPrimary)!.Url
                    ?? p.Images.OrderBy(i => i.DisplayOrder).Select(i => i.Url).FirstOrDefault(),
                null,
                p.CreatedOnUtc))
            .ToListAsync(cancellationToken);

        var productIds = products.Select(p => p.Id).ToList();
        var stockByProduct = await _dbContext.ProductVariants
            .AsNoTracking()
            .Where(v => productIds.Contains(v.ProductId))
            .Join(
                _dbContext.DigitalInventoryCodes.AsNoTracking().Where(c => c.Status == InventoryCodeStatus.Available),
                v => v.Id,
                c => c.VariantId,
                (v, c) => v.ProductId)
            .GroupBy(productId => productId)
            .Select(g => new { ProductId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ProductId, x => x.Count, cancellationToken);

        products = products
            .Select(p => p with { AvailableStock = stockByProduct.GetValueOrDefault(p.Id, 0) })
            .ToList();

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            await TryLogSearchAsync(request.SearchTerm.Trim(), totalCount, cancellationToken);
        }

        return Result.Success(new PagedResult<ProductDto>(products, request.PageNumber, request.PageSize, totalCount));
    }

    private async Task TryLogSearchAsync(string searchTerm, int resultCount, CancellationToken cancellationToken)
    {
        try
        {
            _dbContext.SearchQueryLogs.Add(SearchQueryLog.Create(
                searchTerm,
                resultCount,
                _currentUser.UserId));
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Best-effort search query logging failed.");
        }
    }

    private IQueryable<Domain.Products.Product> ApplySort(
        IQueryable<Domain.Products.Product> query,
        ProductSortBy? sortBy)
    {
        return sortBy switch
        {
            ProductSortBy.PriceAsc => query.OrderBy(p => p.Price).ThenByDescending(p => p.CreatedOnUtc),
            ProductSortBy.PriceDesc => query.OrderByDescending(p => p.Price).ThenByDescending(p => p.CreatedOnUtc),
            ProductSortBy.NameAsc => query.OrderBy(p => p.NameEn).ThenByDescending(p => p.CreatedOnUtc),
            ProductSortBy.NameDesc => query.OrderByDescending(p => p.NameEn).ThenByDescending(p => p.CreatedOnUtc),
            ProductSortBy.CategoryAsc => query
                .OrderBy(p => _dbContext.Categories.FirstOrDefault(c => c.Id == p.CategoryId)!.NameEn)
                .ThenByDescending(p => p.CreatedOnUtc),
            ProductSortBy.CategoryDesc => query
                .OrderByDescending(p => _dbContext.Categories.FirstOrDefault(c => c.Id == p.CategoryId)!.NameEn)
                .ThenByDescending(p => p.CreatedOnUtc),
            ProductSortBy.StatusAsc => query.OrderBy(p => p.Status).ThenByDescending(p => p.CreatedOnUtc),
            ProductSortBy.StatusDesc => query.OrderByDescending(p => p.Status).ThenByDescending(p => p.CreatedOnUtc),
            ProductSortBy.StockAsc => query
                .OrderBy(p => _dbContext.ProductVariants
                    .Where(v => v.ProductId == p.Id)
                    .Join(
                        _dbContext.DigitalInventoryCodes.Where(c => c.Status == InventoryCodeStatus.Available),
                        v => v.Id,
                        c => c.VariantId,
                        (v, c) => c.Id)
                    .Count())
                .ThenByDescending(p => p.CreatedOnUtc),
            ProductSortBy.StockDesc => query
                .OrderByDescending(p => _dbContext.ProductVariants
                    .Where(v => v.ProductId == p.Id)
                    .Join(
                        _dbContext.DigitalInventoryCodes.Where(c => c.Status == InventoryCodeStatus.Available),
                        v => v.Id,
                        c => c.VariantId,
                        (v, c) => c.Id)
                    .Count())
                .ThenByDescending(p => p.CreatedOnUtc),
            ProductSortBy.Newest => query.OrderByDescending(p => p.CreatedOnUtc),
            _ => query.OrderByDescending(p => p.CreatedOnUtc),
        };
    }
}
