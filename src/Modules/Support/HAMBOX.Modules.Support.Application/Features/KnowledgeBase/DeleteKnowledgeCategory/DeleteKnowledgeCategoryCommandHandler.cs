using HAMBOX.Modules.Support.Application.Abstractions;
using HAMBOX.Modules.Support.Application.Errors;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Support.Application.Features.KnowledgeBase.DeleteKnowledgeCategory;

internal sealed class DeleteKnowledgeCategoryCommandHandler(ISupportDbContext dbContext)
    : IRequestHandler<DeleteKnowledgeCategoryCommand, Result>
{
    public async Task<Result> Handle(DeleteKnowledgeCategoryCommand request, CancellationToken cancellationToken)
    {
        var category = await dbContext.KnowledgeCategories.FirstOrDefaultAsync(c => c.Id == request.CategoryId, cancellationToken);
        if (category is null)
        {
            return Result.Failure(SupportErrors.KnowledgeCategoryNotFound);
        }

        category.Delete();
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
