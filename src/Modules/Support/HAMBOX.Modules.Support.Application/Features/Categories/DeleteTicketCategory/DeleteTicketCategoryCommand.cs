using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Support.Application.Features.Categories.DeleteTicketCategory;

public sealed record DeleteTicketCategoryCommand(Guid CategoryId) : IRequest<Result>;
