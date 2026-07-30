using HAMBOX.Modules.Support.Application.Contracts;
using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Support.Application.Features.Categories.GetTicketCategories;

public sealed record GetTicketCategoriesQuery : IRequest<Result<IReadOnlyList<TicketCategoryDto>>>;
