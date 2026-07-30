using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Support.Application.Features.Tickets.AssignTicket;

/// <summary>Covers both "assign" (first assignment) and "transfer" (re-assignment) — same
/// operation from the domain's perspective, see plan decision on collapsing the two.</summary>
public sealed record AssignTicketCommand(Guid TicketId, string AgentUserId, string AssignedByUserId) : IRequest<Result>;
