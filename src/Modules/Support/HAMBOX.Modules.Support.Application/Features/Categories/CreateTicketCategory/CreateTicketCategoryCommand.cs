using HAMBOX.Modules.Support.Application.Contracts;
using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Support.Application.Features.Categories.CreateTicketCategory;

public sealed record CreateTicketCategoryCommand(string Name, string Color, string Icon, int SortOrder)
    : IRequest<Result<TicketCategoryDto>>;
