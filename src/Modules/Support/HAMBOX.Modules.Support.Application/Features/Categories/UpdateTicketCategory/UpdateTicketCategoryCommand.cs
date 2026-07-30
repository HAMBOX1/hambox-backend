using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Support.Application.Features.Categories.UpdateTicketCategory;

public sealed record UpdateTicketCategoryCommand(
    Guid CategoryId, string Name, string Color, string Icon, int SortOrder, bool IsActive) : IRequest<Result>;
