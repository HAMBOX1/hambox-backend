using HAMBOX.Modules.Support.Application.Abstractions;
using HAMBOX.Modules.Support.Application.Contracts;
using HAMBOX.Modules.Support.Application.Services;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Support.Application.Features.KnowledgeBase.GetKnowledgeCategories;

internal sealed class GetKnowledgeCategoriesQueryHandler(ISupportDbContext dbContext)
    : IRequestHandler<GetKnowledgeCategoriesQuery, Result<IReadOnlyList<KnowledgeCategoryDto>>>
{
    public async Task<Result<IReadOnlyList<KnowledgeCategoryDto>>> Handle(GetKnowledgeCategoriesQuery request, CancellationToken cancellationToken)
    {
        var categories = await dbContext.KnowledgeCategories.AsNoTracking().OrderBy(c => c.SortOrder).ToListAsync(cancellationToken);
        return Result.Success<IReadOnlyList<KnowledgeCategoryDto>>(categories.Select(KnowledgeBaseMapper.ToDto).ToList());
    }
}
