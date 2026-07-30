using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Support.Application.Features.KnowledgeBase.DeleteKnowledgeCategory;

public sealed record DeleteKnowledgeCategoryCommand(Guid CategoryId) : IRequest<Result>;
