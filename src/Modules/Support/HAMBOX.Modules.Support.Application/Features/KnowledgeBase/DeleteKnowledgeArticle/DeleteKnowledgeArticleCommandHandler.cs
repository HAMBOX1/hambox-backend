using HAMBOX.Modules.Support.Application.Abstractions;
using HAMBOX.Modules.Support.Application.Errors;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Support.Application.Features.KnowledgeBase.DeleteKnowledgeArticle;

internal sealed class DeleteKnowledgeArticleCommandHandler(ISupportDbContext dbContext)
    : IRequestHandler<DeleteKnowledgeArticleCommand, Result>
{
    public async Task<Result> Handle(DeleteKnowledgeArticleCommand request, CancellationToken cancellationToken)
    {
        var article = await dbContext.KnowledgeArticles.FirstOrDefaultAsync(a => a.Id == request.ArticleId, cancellationToken);
        if (article is null)
        {
            return Result.Failure(SupportErrors.KnowledgeArticleNotFound);
        }

        article.Delete();
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
