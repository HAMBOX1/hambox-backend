using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Support.Application.Features.Tickets.DeleteTicket;

public sealed record DeleteTicketCommand(Guid TicketId, string DeletedByUserId) : IRequest<Result>;
