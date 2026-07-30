using HAMBOX.Modules.Support.Application.Contracts;
using HAMBOX.Modules.Support.Domain.KnowledgeBase;
using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Support.Application.Features.KnowledgeBase.GetKnowledgeArticles;

/// <summary>When <paramref name="PublicOnly"/> is set (storefront KB endpoint), only Published +
/// Public articles are returned regardless of the other filters.</summary>
public sealed record GetKnowledgeArticlesQuery(
    bool PublicOnly,
    int Page,
    int PageSize,
    string? Search,
    Guid? CategoryId,
    KnowledgeArticleStatus? Status) : IRequest<Result<PagedResult<KnowledgeArticleSummaryDto>>>;
