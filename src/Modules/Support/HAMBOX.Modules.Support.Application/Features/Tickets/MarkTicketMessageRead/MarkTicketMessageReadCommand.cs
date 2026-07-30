using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Support.Application.Features.Tickets.MarkTicketMessageRead;

public sealed record MarkTicketMessageReadCommand(Guid TicketId, Guid MessageId, string ReaderUserId) : IRequest<Result>;
