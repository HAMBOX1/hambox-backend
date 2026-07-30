using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Support.Application.Features.Tags.RemoveTagFromTicket;

public sealed record RemoveTagFromTicketCommand(Guid TicketId, Guid TagId, string ActorUserId) : IRequest<Result>;
