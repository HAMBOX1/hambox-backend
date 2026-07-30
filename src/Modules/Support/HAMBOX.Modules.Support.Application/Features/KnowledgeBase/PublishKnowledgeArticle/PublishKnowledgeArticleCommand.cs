using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Support.Application.Features.KnowledgeBase.PublishKnowledgeArticle;

public sealed record PublishKnowledgeArticleCommand(Guid ArticleId) : IRequest<Result>;
