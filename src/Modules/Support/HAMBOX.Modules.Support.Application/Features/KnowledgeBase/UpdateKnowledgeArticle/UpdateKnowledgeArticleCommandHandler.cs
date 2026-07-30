using System.Text.Json;
using HAMBOX.Modules.Support.Application.Abstractions;
using HAMBOX.Modules.Support.Application.Errors;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Support.Application.Features.KnowledgeBase.UpdateKnowledgeArticle;

internal sealed class UpdateKnowledgeArticleCommandHandler(ISupportDbContext dbContext)
    : IRequestHandler<UpdateKnowledgeArticleCommand, Result>
{
    public async Task<Result> Handle(UpdateKnowledgeArticleCommand request, CancellationToken cancellationToken)
    {
        var article = await dbContext.KnowledgeArticles.FirstOrDefaultAsync(a => a.Id == request.ArticleId, cancellationToken);
        if (article is null)
        {
            return Result.Failure(SupportErrors.KnowledgeArticleNotFound);
        }

        var categoryExists = await dbContext.KnowledgeCategories.AnyAsync(c => c.Id == request.CategoryId, cancellationToken);
        if (!categoryExists)
        {
            return Result.Failure(SupportErrors.KnowledgeCategoryNotFound);
        }

        var relatedJson = request.RelatedArticleIds is { Count: > 0 } ? JsonSerializer.Serialize(request.RelatedArticleIds) : null;
        article.Update(request.CategoryId, request.Title, request.Body, request.Visibility, relatedJson);

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
