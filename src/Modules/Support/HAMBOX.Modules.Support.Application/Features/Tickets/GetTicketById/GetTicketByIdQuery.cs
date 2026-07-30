using HAMBOX.Modules.Support.Application.Contracts;
using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Support.Application.Features.Tickets.GetTicketById;

/// <summary>When <paramref name="RequestingCustomerUserId"/> is set, internal notes are stripped
/// and ownership is enforced (customer endpoint); null means an agent/admin call.</summary>
public sealed record GetTicketByIdQuery(Guid TicketId, string? RequestingCustomerUserId) : IRequest<Result<TicketDetailDto>>;
