using HAMBOX.Modules.Support.Application.Contracts;
using HAMBOX.Modules.Support.Domain.KnowledgeBase;
using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Support.Application.Features.KnowledgeBase.CreateKnowledgeArticle;

public sealed record CreateKnowledgeArticleCommand(
    Guid CategoryId, string Title, string Body, KnowledgeArticleVisibility Visibility) : IRequest<Result<KnowledgeArticleDetailDto>>;
