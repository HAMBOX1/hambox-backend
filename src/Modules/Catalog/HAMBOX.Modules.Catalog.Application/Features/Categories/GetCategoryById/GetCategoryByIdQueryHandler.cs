using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Catalog.Application.Contracts;
using HAMBOX.Modules.Catalog.Application.Errors;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Threading;
using System.Threading.Tasks;

namespace HAMBOX.Modules.Catalog.Application.Features.Categories.GetCategoryById;

internal sealed class GetCategoryByIdQueryHandler : IRequestHandler<GetCategoryByIdQuery, Result<CategoryDto>>
{
    private readonly ICatalogDbContext _dbContext;

    public GetCategoryByIdQueryHandler(ICatalogDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<CategoryDto>> Handle(GetCategoryByIdQuery request, CancellationToken cancellationToken)
    {
        var category = await _dbContext.Categories
            .AsNoTracking()
            .Where(c => c.Id == request.Id)
            .Select(c => new CategoryDto(c.Id, c.NameAr, c.NameEn, c.Slug, c.IsActive, c.ParentId, c.ImageUrl))
            .FirstOrDefaultAsync(cancellationToken);

        if (category is null)
        {
            return Result.Failure<CategoryDto>(CatalogErrors.CategoryNotFound);
        }

        return Result.Success(category);
    }
}
