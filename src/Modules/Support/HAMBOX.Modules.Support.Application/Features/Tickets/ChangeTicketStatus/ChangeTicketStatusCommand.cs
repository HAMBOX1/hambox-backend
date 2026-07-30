using HAMBOX.Modules.Support.Domain.Tickets;
using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Support.Application.Features.Tickets.ChangeTicketStatus;

public sealed record ChangeTicketStatusCommand(Guid TicketId, TicketStatus NewStatus, string ChangedByUserId) : IRequest<Result>;
