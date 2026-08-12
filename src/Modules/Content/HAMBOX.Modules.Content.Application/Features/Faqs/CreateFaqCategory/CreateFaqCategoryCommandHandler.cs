using HAMBOX.Modules.Content.Application.Abstractions;
using HAMBOX.Modules.Content.Application.Contracts.Faqs;
using HAMBOX.Modules.Content.Domain.Faqs;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Content.Application.Features.Faqs.CreateFaqCategory;

internal sealed class CreateFaqCategoryCommandHandler(IContentDbContext dbContext)
    : IRequestHandler<CreateFaqCategoryCommand, Result<FaqCategoryDto>>
{
    public async Task<Result<FaqCategoryDto>> Handle(CreateFaqCategoryCommand request, CancellationToken cancellationToken)
    {
        var maxSortOrder = await dbContext.FaqCategories.MaxAsync(c => (int?)c.SortOrder, cancellationToken) ?? -1;

        var category = FaqCategory.Create(request.NameEn, request.NameAr, maxSortOrder + 1);
        dbContext.FaqCategories.Add(category);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(new FaqCategoryDto(category.Id, category.NameEn, category.NameAr, category.Slug, category.SortOrder));
    }
}
