using HAMBOX.Modules.Support.Application.Abstractions;
using HAMBOX.Modules.Support.Application.Contracts;
using HAMBOX.Modules.Support.Application.Services;
using HAMBOX.Modules.Support.Domain.KnowledgeBase;
using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Support.Application.Features.KnowledgeBase.CreateKnowledgeCategory;

internal sealed class CreateKnowledgeCategoryCommandHandler(ISupportDbContext dbContext)
    : IRequestHandler<CreateKnowledgeCategoryCommand, Result<KnowledgeCategoryDto>>
{
    public async Task<Result<KnowledgeCategoryDto>> Handle(CreateKnowledgeCategoryCommand request, CancellationToken cancellationToken)
    {
        var category = KnowledgeCategory.Create(request.Name, request.SortOrder);
        dbContext.KnowledgeCategories.Add(category);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(KnowledgeBaseMapper.ToDto(category));
    }
}
