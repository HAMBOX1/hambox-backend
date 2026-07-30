using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Support.Application.Features.KnowledgeBase.UnpublishKnowledgeArticle;

public sealed record UnpublishKnowledgeArticleCommand(Guid ArticleId) : IRequest<Result>;
