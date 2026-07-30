using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Support.Application.Features.Tickets.RateTicket;

public sealed record RateTicketCommand(Guid TicketId, string CustomerUserId, int Score, string? Comment) : IRequest<Result>;
