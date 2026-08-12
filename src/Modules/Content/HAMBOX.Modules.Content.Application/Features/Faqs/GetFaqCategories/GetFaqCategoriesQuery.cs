using HAMBOX.Modules.Content.Application.Contracts.Faqs;
using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Content.Application.Features.Faqs.GetFaqCategories;

public sealed record GetFaqCategoriesQuery : IRequest<Result<IReadOnlyList<FaqCategoryDto>>>;
