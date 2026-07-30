using HAMBOX.Modules.Support.Application.Contracts;
using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Support.Application.Features.KnowledgeBase.CreateKnowledgeCategory;

public sealed record CreateKnowledgeCategoryCommand(string Name, int SortOrder) : IRequest<Result<KnowledgeCategoryDto>>;
