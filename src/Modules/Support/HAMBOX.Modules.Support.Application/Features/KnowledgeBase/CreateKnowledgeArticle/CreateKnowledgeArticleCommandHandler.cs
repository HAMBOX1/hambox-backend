using HAMBOX.Modules.Support.Application.Abstractions;
using HAMBOX.Modules.Support.Application.Contracts;
using HAMBOX.Modules.Support.Application.Errors;
using HAMBOX.Modules.Support.Domain.KnowledgeBase;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Support.Application.Features.KnowledgeBase.CreateKnowledgeArticle;

internal sealed class CreateKnowledgeArticleCommandHandler(ISupportDbContext dbContext)
    : IRequestHandler<CreateKnowledgeArticleCommand, Result<KnowledgeArticleDetailDto>>
{
    public async Task<Result<KnowledgeArticleDetailDto>> Handle(CreateKnowledgeArticleCommand request, CancellationToken cancellationToken)
    {
        var category = await dbContext.KnowledgeCategories.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == request.CategoryId, cancellationToken);
        if (category is null)
        {
            return Result.Failure<KnowledgeArticleDetailDto>(SupportErrors.KnowledgeCategoryNotFound);
        }

        var article = KnowledgeArticle.Create(request.CategoryId, request.Title, request.Body, request.Visibility);
        dbContext.KnowledgeArticles.Add(article);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(new KnowledgeArticleDetailDto(
            article.Id, article.Title, article.Slug, article.Body, article.CategoryId, category.Name,
            article.Status, article.Visibility, article.ViewCount, [], article.PublishedOnUtc));
    }
}
