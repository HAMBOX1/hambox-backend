using HAMBOX.Modules.Support.Application.Contracts;
using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Support.Application.Features.KnowledgeBase.GetKnowledgeArticleById;

/// <summary>When <paramref name="RecordView"/> is set (storefront read endpoint), the article's
/// view count is incremented; admin preview calls should pass false.</summary>
public sealed record GetKnowledgeArticleByIdQuery(Guid ArticleId, bool RecordView) : IRequest<Result<KnowledgeArticleDetailDto>>;
