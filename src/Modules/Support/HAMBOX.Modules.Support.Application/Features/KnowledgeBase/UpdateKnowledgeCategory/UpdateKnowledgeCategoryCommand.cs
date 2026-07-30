using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Support.Application.Features.KnowledgeBase.UpdateKnowledgeCategory;

public sealed record UpdateKnowledgeCategoryCommand(Guid CategoryId, string Name, int SortOrder, bool IsActive) : IRequest<Result>;
