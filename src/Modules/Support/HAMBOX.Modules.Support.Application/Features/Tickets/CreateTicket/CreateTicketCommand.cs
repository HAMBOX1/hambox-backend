using HAMBOX.Modules.Support.Application.Contracts;
using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Support.Application.Features.Tickets.CreateTicket;

/// <summary>
/// Creates a ticket. Used by both the customer self-service endpoint (CustomerUserId ==
/// CreatedByUserId) and the agent "create on behalf of" endpoint (Support.Create permission,
/// CreatedByUserId is the agent) — one command, the endpoint layer decides who may set what.
/// </summary>
public sealed record CreateTicketCommand(
    string CustomerUserId,
    string CreatedByUserId,
    string Subject,
    string Body,
    Guid? CategoryId,
    Guid? PriorityId,
    Guid? RelatedOrderId,
    Guid? RelatedProductId,
    string? CustomerCountry,
    string? CustomerBrowser,
    string? CustomerDevice,
    string? CustomerIpAddress) : IRequest<Result<TicketDetailDto>>;
