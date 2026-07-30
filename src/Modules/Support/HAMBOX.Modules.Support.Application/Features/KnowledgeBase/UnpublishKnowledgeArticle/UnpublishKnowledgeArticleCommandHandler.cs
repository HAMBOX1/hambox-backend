using HAMBOX.Modules.Support.Application.Abstractions;
using HAMBOX.Modules.Support.Application.Errors;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Support.Application.Features.KnowledgeBase.UnpublishKnowledgeArticle;

internal sealed class UnpublishKnowledgeArticleCommandHandler(ISupportDbContext dbContext)
    : IRequestHandler<UnpublishKnowledgeArticleCommand, Result>
{
    public async Task<Result> Handle(UnpublishKnowledgeArticleCommand request, CancellationToken cancellationToken)
    {
        var article = await dbContext.KnowledgeArticles.FirstOrDefaultAsync(a => a.Id == request.ArticleId, cancellationToken);
        if (article is null)
        {
            return Result.Failure(SupportErrors.KnowledgeArticleNotFound);
        }

        article.Unpublish();
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
