using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Catalog.Application.Contracts;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Catalog.Application.Features.Categories.GetCategories;

internal sealed class GetCategoriesQueryHandler : IRequestHandler<GetCategoriesQuery, Result<PagedResult<CategoryDto>>>
{
    private readonly ICatalogDbContext _dbContext;

    public GetCategoriesQueryHandler(ICatalogDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<PagedResult<CategoryDto>>> Handle(GetCategoriesQuery request, CancellationToken cancellationToken)
    {
        var query = _dbContext.Categories.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            query = query.Where(c => c.NameAr.Contains(request.SearchTerm) || c.NameEn.Contains(request.SearchTerm));
        }

        if (request.ActiveOnly.HasValue)
        {
            query = query.Where(c => c.IsActive == request.ActiveOnly.Value);
        }

        int totalCount = await query.CountAsync(cancellationToken);

        var categories = await query
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(c => new CategoryDto(c.Id, c.NameAr, c.NameEn, c.Slug, c.IsActive, c.ParentId))
            .ToListAsync(cancellationToken);

        return Result.Success(new PagedResult<CategoryDto>(categories, request.PageNumber, request.PageSize, totalCount));
    }
}
