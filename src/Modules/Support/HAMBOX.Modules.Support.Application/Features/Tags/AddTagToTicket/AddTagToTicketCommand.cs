using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Support.Application.Features.Tags.AddTagToTicket;

public sealed record AddTagToTicketCommand(Guid TicketId, Guid TagId, string ActorUserId) : IRequest<Result>;
