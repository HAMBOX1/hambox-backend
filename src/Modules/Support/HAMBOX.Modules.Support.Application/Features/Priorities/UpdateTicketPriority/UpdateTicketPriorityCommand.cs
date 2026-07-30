using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Support.Application.Features.Priorities.UpdateTicketPriority;

public sealed record UpdateTicketPriorityCommand(
    Guid PriorityId, string Name, string Color, int Level,
    int? SlaFirstResponseMinutes, int? SlaResolutionMinutes, bool IsActive) : IRequest<Result>;
