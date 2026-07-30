using HAMBOX.Modules.Support.Application.Abstractions;
using HAMBOX.Modules.Support.Application.Errors;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Support.Application.Features.KnowledgeBase.UpdateKnowledgeCategory;

internal sealed class UpdateKnowledgeCategoryCommandHandler(ISupportDbContext dbContext)
    : IRequestHandler<UpdateKnowledgeCategoryCommand, Result>
{
    public async Task<Result> Handle(UpdateKnowledgeCategoryCommand request, CancellationToken cancellationToken)
    {
        var category = await dbContext.KnowledgeCategories.FirstOrDefaultAsync(c => c.Id == request.CategoryId, cancellationToken);
        if (category is null)
        {
            return Result.Failure(SupportErrors.KnowledgeCategoryNotFound);
        }

        category.Update(request.Name, request.SortOrder, request.IsActive);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
