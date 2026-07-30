using HAMBOX.Modules.Support.Application.Contracts;
using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Support.Application.Features.Priorities.CreateTicketPriority;

public sealed record CreateTicketPriorityCommand(
    string Name, string Color, int Level, int? SlaFirstResponseMinutes, int? SlaResolutionMinutes)
    : IRequest<Result<TicketPriorityDto>>;
