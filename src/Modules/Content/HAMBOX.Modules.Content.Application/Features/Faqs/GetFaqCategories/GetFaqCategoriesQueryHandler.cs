using HAMBOX.Modules.Content.Application.Abstractions;
using HAMBOX.Modules.Content.Application.Contracts.Faqs;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Content.Application.Features.Faqs.GetFaqCategories;

internal sealed class GetFaqCategoriesQueryHandler(IContentDbContext dbContext)
    : IRequestHandler<GetFaqCategoriesQuery, Result<IReadOnlyList<FaqCategoryDto>>>
{
    public async Task<Result<IReadOnlyList<FaqCategoryDto>>> Handle(GetFaqCategoriesQuery request, CancellationToken cancellationToken)
    {
        var categories = await dbContext.FaqCategories
            .AsNoTracking()
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.NameEn)
            .Select(c => new FaqCategoryDto(c.Id, c.NameEn, c.NameAr, c.Slug, c.SortOrder))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<FaqCategoryDto>>(categories);
    }
}
