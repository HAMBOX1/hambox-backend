using System.Text.Json;
using HAMBOX.Modules.Support.Application.Abstractions;
using HAMBOX.Modules.Support.Application.Contracts;
using HAMBOX.Modules.Support.Application.Errors;
using HAMBOX.Modules.Support.Application.Services;
using HAMBOX.Modules.Support.Domain.KnowledgeBase;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Support.Application.Features.KnowledgeBase.GetKnowledgeArticleById;

internal sealed class GetKnowledgeArticleByIdQueryHandler(ISupportDbContext dbContext)
    : IRequestHandler<GetKnowledgeArticleByIdQuery, Result<KnowledgeArticleDetailDto>>
{
    public async Task<Result<KnowledgeArticleDetailDto>> Handle(GetKnowledgeArticleByIdQuery request, CancellationToken cancellationToken)
    {
        var article = await dbContext.KnowledgeArticles.FirstOrDefaultAsync(a => a.Id == request.ArticleId, cancellationToken);
        if (article is null)
        {
            return Result.Failure<KnowledgeArticleDetailDto>(SupportErrors.KnowledgeArticleNotFound);
        }

        // The public reader (RecordView) may only open what the public list shows. A draft or an Internal (agent-only)
        // article answers exactly like a missing one, so its existence is not revealed either.
        var publicReader = request.RecordView;
        if (publicReader && (article.Status != KnowledgeArticleStatus.Published || article.Visibility != KnowledgeArticleVisibility.Public))
        {
            return Result.Failure<KnowledgeArticleDetailDto>(SupportErrors.KnowledgeArticleNotFound);
        }

        if (request.RecordView)
        {
            article.RecordView();
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var category = await dbContext.KnowledgeCategories.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == article.CategoryId, cancellationToken);

        var relatedIds = string.IsNullOrWhiteSpace(article.RelatedArticleIdsJson)
            ? []
            : JsonSerializer.Deserialize<List<Guid>>(article.RelatedArticleIdsJson) ?? [];

        var relatedArticles = relatedIds.Count == 0
            ? []
            : await dbContext.KnowledgeArticles.AsNoTracking()
                .Where(a => relatedIds.Contains(a.Id)
                    && (!publicReader || (a.Status == KnowledgeArticleStatus.Published && a.Visibility == KnowledgeArticleVisibility.Public)))
                .ToListAsync(cancellationToken);

        var relatedCategoryIds = relatedArticles.Select(a => a.CategoryId).Distinct().ToList();
        var relatedCategories = await dbContext.KnowledgeCategories.AsNoTracking()
            .Where(c => relatedCategoryIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.Name, cancellationToken);

        var relatedDtos = relatedArticles
            .Select(a => KnowledgeBaseMapper.ToSummaryDto(a, relatedCategories.GetValueOrDefault(a.CategoryId, "Unknown")))
            .ToList();

        return Result.Success(new KnowledgeArticleDetailDto(
            article.Id, article.Title, article.Slug, article.Body, article.CategoryId, category?.Name ?? "Unknown",
            article.Status, article.Visibility, article.ViewCount, relatedDtos, article.PublishedOnUtc));
    }
}
