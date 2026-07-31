using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Catalog.Application.Features.ImportExport.GetCatalogPackageJobs;

internal sealed class GetCatalogPackageJobsQueryHandler(ICatalogDbContext dbContext)
    : IRequestHandler<GetCatalogPackageJobsQuery, Result<PagedResult<CatalogPackageJobDto>>>
{
    public async Task<Result<PagedResult<CatalogPackageJobDto>>> Handle(
        GetCatalogPackageJobsQuery request, CancellationToken cancellationToken)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize <= 0 ? 25 : request.PageSize, 1, 100);

        var query = dbContext.CatalogPackageJobs.AsNoTracking().AsQueryable();

        if (request.Direction.HasValue)
        {
            query = query.Where(j => j.Direction == request.Direction.Value);
        }

        if (request.Status.HasValue)
        {
            query = query.Where(j => j.Status == request.Status.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            query = query.Where(j =>
                j.FileName.Contains(term)
                || (j.ResultFileName != null && j.ResultFileName.Contains(term)));
        }

        query = (request.Sort?.ToLowerInvariant()) switch
        {
            "created_asc" => query.OrderBy(j => j.CreatedOnUtc),
            _ => query.OrderByDescending(j => j.CreatedOnUtc),
        };

        var total = await query.CountAsync(cancellationToken);
        var rows = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = rows.Select(CatalogPackageJobMapper.ToDto).ToList();

        return Result.Success(new PagedResult<CatalogPackageJobDto>(items, page, pageSize, total));
    }
}
