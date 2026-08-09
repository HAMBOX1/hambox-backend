using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Catalog.Application.Contracts;
using HAMBOX.Modules.Catalog.Application.Errors;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Catalog.Application.Features.Categories.GetCategoryBySlug;

internal sealed class GetCategoryBySlugQueryHandler(ICatalogDbContext dbContext)
    : IRequestHandler<GetCategoryBySlugQuery, Result<CategoryDto>>
{
    public async Task<Result<CategoryDto>> Handle(GetCategoryBySlugQuery request, CancellationToken cancellationToken)
    {
        var category = await dbContext.Categories
            .AsNoTracking()
            .Where(c => c.Slug == request.Slug)
            .Select(c => new CategoryDto(c.Id, c.NameAr, c.NameEn, c.Slug, c.IsActive, c.ParentId, c.ImageUrl))
            .FirstOrDefaultAsync(cancellationToken);

        if (category is null)
        {
            return Result.Failure<CategoryDto>(CatalogErrors.CategoryNotFound);
        }

        return Result.Success(category);
    }
}
