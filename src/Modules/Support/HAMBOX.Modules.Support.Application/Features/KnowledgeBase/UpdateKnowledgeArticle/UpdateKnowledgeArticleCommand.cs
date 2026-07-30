using HAMBOX.Modules.Support.Domain.KnowledgeBase;
using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Support.Application.Features.KnowledgeBase.UpdateKnowledgeArticle;

public sealed record UpdateKnowledgeArticleCommand(
    Guid ArticleId, Guid CategoryId, string Title, string Body, KnowledgeArticleVisibility Visibility,
    IReadOnlyList<Guid>? RelatedArticleIds) : IRequest<Result>;
