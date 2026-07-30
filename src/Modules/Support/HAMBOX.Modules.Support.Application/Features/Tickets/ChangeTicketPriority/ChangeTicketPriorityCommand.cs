using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Support.Application.Features.Tickets.ChangeTicketPriority;

public sealed record ChangeTicketPriorityCommand(Guid TicketId, Guid? PriorityId, string ChangedByUserId) : IRequest<Result>;
