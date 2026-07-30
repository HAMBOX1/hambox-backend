using HAMBOX.Modules.Support.Application.Abstractions;
using HAMBOX.Modules.Support.Application.Contracts;
using HAMBOX.Modules.Support.Domain.KnowledgeBase;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Support.Application.Features.KnowledgeBase.GetKnowledgeArticles;

internal sealed class GetKnowledgeArticlesQueryHandler(ISupportDbContext dbContext)
    : IRequestHandler<GetKnowledgeArticlesQuery, Result<PagedResult<KnowledgeArticleSummaryDto>>>
{
    public async Task<Result<PagedResult<KnowledgeArticleSummaryDto>>> Handle(
        GetKnowledgeArticlesQuery request, CancellationToken cancellationToken)
    {
        var query = dbContext.KnowledgeArticles.AsNoTracking().AsQueryable();

        if (request.PublicOnly)
        {
            query = query.Where(a => a.Status == KnowledgeArticleStatus.Published && a.Visibility == KnowledgeArticleVisibility.Public);
        }
        else if (request.Status is not null)
        {
            query = query.Where(a => a.Status == request.Status);
        }

        if (request.CategoryId is not null)
        {
            query = query.Where(a => a.CategoryId == request.CategoryId);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            query = query.Where(a => a.Title.Contains(term) || a.Body.Contains(term));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var articles = await query
            .OrderByDescending(a => a.PublishedOnUtc ?? a.CreatedOnUtc)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var categoryIds = articles.Select(a => a.CategoryId).Distinct().ToList();
        var categories = await dbContext.KnowledgeCategories.AsNoTracking()
            .Where(c => categoryIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.Name, cancellationToken);

        var summaries = articles
            .Select(a => new KnowledgeArticleSummaryDto(
                a.Id, a.Title, a.Slug, a.CategoryId, categories.GetValueOrDefault(a.CategoryId, "Unknown"),
                a.Status, a.Visibility, a.ViewCount, a.PublishedOnUtc))
            .ToList();

        return Result.Success(new PagedResult<KnowledgeArticleSummaryDto>(summaries, request.Page, request.PageSize, totalCount));
    }
}
