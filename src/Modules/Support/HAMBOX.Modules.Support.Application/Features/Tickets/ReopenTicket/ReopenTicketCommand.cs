using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Support.Application.Features.Tickets.ReopenTicket;

public sealed record ReopenTicketCommand(Guid TicketId, string RequestedByUserId, bool IsCustomerRequest) : IRequest<Result>;
