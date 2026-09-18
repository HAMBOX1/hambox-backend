using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Catalog.Application.Features.Products;
using HAMBOX.Modules.Catalog.Domain.Enums;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Catalog.Application.Features.Products.GetProductStatusCounts;

internal sealed class GetProductStatusCountsQueryHandler
    : IRequestHandler<GetProductStatusCountsQuery, Result<ProductStatusCountsDto>>
{
    private readonly ICatalogDbContext _dbContext;

    public GetProductStatusCountsQueryHandler(ICatalogDbContext dbContext) => _dbContext = dbContext;

    public async Task<Result<ProductStatusCountsDto>> Handle(GetProductStatusCountsQuery request, CancellationToken cancellationToken)
    {
        var query = ProductQueryFilters.ApplyBaseFilters(
            _dbContext.Products.AsNoTracking(), request.SearchTerm, request.CategoryId, status: null, request.CollectionId);

        var counts = await query
            .GroupBy(p => p.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Status, x => x.Count, cancellationToken);

        int Get(ProductStatus status) => counts.GetValueOrDefault(status, 0);

        var pendingMergeCount = await ProductQueryFilters.ApplyBaseFilters(
                _dbContext.Products.AsNoTracking(), request.SearchTerm, request.CategoryId, status: null, request.CollectionId, includePendingMerge: true)
            .CountAsync(p => p.PendingMergeIntoProductId != null, cancellationToken);

        return Result.Success(new ProductStatusCountsDto(
            All: counts.Values.Sum(),
            Draft: Get(ProductStatus.Draft),
            Active: Get(ProductStatus.Active),
            Inactive: Get(ProductStatus.Inactive),
            Archived: Get(ProductStatus.Archived),
            PendingMerge: pendingMergeCount));
    }
}
