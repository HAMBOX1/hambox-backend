using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Support.Application.Features.Tickets.MergeTickets;

public sealed record MergeTicketsCommand(Guid SourceTicketId, Guid TargetTicketId, string MergedByUserId) : IRequest<Result>;
