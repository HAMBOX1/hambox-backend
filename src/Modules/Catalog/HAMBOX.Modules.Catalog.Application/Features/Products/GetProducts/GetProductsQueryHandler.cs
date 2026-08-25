using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HAMBOX.Application.Abstractions;
using HAMBOX.Application.Membership;
using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Catalog.Application.Contracts;
using HAMBOX.Modules.Catalog.Application.Features.Products.GetProductById;
using HAMBOX.Modules.Catalog.Application.Services;
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
    private readonly IMembershipAccessProvider _membershipAccess;
    private readonly ILogger<GetProductsQueryHandler> _logger;

    public GetProductsQueryHandler(
        ICatalogDbContext dbContext,
        ICurrentUserService currentUser,
        IMembershipAccessProvider membershipAccess,
        ILogger<GetProductsQueryHandler> logger)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _membershipAccess = membershipAccess;
        _logger = logger;
    }

    public async Task<Result<PagedResult<ProductDto>>> Handle(GetProductsQuery request, CancellationToken cancellationToken)
    {
        var query = ProductQueryFilters.ApplyBaseFilters(
            _dbContext.Products.AsNoTracking(), request.SearchTerm, request.CategoryId, request.Status, request.CollectionId);

        query = ProductQueryFilters.ApplyAttributeFilters(query, _dbContext, request.AttributeFilters);

        query = ApplySort(query, request.SortBy);

        var totalCount = await query.CountAsync(cancellationToken);

        var rows = await query
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(p => new
            {
                p.Id,
                p.NameAr,
                p.NameEn,
                p.DescriptionAr,
                p.DescriptionEn,
                p.Price,
                Status = p.Status.ToString(),
                p.CategoryId,
                CategoryName = _dbContext.Categories.FirstOrDefault(c => c.Id == p.CategoryId)!.NameEn,
                CategoryNameAr = _dbContext.Categories.FirstOrDefault(c => c.Id == p.CategoryId)!.NameAr,
                AdditionalCategoryIds = p.AdditionalCategories.Select(pc => pc.CategoryId).ToList(),
                ProductImageUrl = p.Images.FirstOrDefault(i => i.IsPrimary)!.Url
                    ?? p.Images.OrderBy(i => i.DisplayOrder).Select(i => i.Url).FirstOrDefault(),
                CategoryOwnImageUrl = _dbContext.Categories.FirstOrDefault(c => c.Id == p.CategoryId)!.ImageUrl,
                CategoryEffectiveImageUrl = _dbContext.Categories.FirstOrDefault(c => c.Id == p.CategoryId)!.EffectiveImageUrl,
                p.CreatedOnUtc,
                p.PublicReleaseOnUtc,
                CollectionIds = p.Collections.Select(pc => pc.CollectionId).ToList(),
                p.LastEditedByName,
                p.LastEditedOnUtc,
            })
            .ToListAsync(cancellationToken);

        var rowIds = rows.Select(r => r.Id).ToList();
        var productAccessById = await _membershipAccess.GetProductsAccessAsync(_currentUser.UserId, rowIds, cancellationToken);
        var membershipAccess = await _membershipAccess.GetAccessInfoAsync(_currentUser.UserId, cancellationToken);

        var products = rows
            .Select(r =>
            {
                var displayImage = ProductDisplayImageResolver.Resolve(
                    r.ProductImageUrl, r.CategoryOwnImageUrl, r.CategoryEffectiveImageUrl);
                var access = productAccessById.GetValueOrDefault(r.Id, ProductAccessInfo.Unrestricted);
                var canPurchase = access.HasAccess
                    && GetProductByIdQueryHandler.IsReleasedFor(r.PublicReleaseOnUtc, membershipAccess);

                return new ProductDto(
                    r.Id,
                    r.NameAr,
                    r.NameEn,
                    r.DescriptionAr,
                    r.DescriptionEn,
                    r.Price,
                    r.Status,
                    r.CategoryId,
                    r.CategoryName,
                    r.CategoryNameAr,
                    r.AdditionalCategoryIds,
                    r.ProductImageUrl,
                    null,
                    r.CreatedOnUtc,
                    0,
                    r.CollectionIds,
                    displayImage.Url,
                    displayImage.Source,
                    r.PublicReleaseOnUtc,
                    access.IsRestricted,
                    canPurchase,
                    access.RequiredPlanNames,
                    LastEditedByName: _currentUser.IsAdminContext ? r.LastEditedByName : null,
                    LastEditedOnUtc: _currentUser.IsAdminContext ? r.LastEditedOnUtc : null);
            })
            .ToList();

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
