using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Support.Application.Features.Priorities.DeleteTicketPriority;

public sealed record DeleteTicketPriorityCommand(Guid PriorityId) : IRequest<Result>;
