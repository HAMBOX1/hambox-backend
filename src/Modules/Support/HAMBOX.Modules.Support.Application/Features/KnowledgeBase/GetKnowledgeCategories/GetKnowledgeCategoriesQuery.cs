using HAMBOX.Modules.Support.Application.Contracts;
using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Support.Application.Features.KnowledgeBase.GetKnowledgeCategories;

public sealed record GetKnowledgeCategoriesQuery : IRequest<Result<IReadOnlyList<KnowledgeCategoryDto>>>;
