using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Support.Application.Features.KnowledgeBase.DeleteKnowledgeArticle;

public sealed record DeleteKnowledgeArticleCommand(Guid ArticleId) : IRequest<Result>;
