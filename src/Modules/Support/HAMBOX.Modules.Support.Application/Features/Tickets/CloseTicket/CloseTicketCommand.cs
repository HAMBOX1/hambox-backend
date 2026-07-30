using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Support.Application.Features.Tickets.CloseTicket;

/// <summary>Closes a ticket. <paramref name="IsCustomerRequest"/> gates ownership enforcement —
/// a customer may only close their own ticket, an agent (permission-gated at the endpoint) may
/// close any.</summary>
public sealed record CloseTicketCommand(Guid TicketId, string RequestedByUserId, bool IsCustomerRequest) : IRequest<Result>;
