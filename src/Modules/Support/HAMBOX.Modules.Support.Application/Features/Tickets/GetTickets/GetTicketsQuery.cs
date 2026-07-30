using HAMBOX.Modules.Support.Application.Contracts;
using HAMBOX.Modules.Support.Domain.Tickets;
using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Support.Application.Features.Tickets.GetTickets;

/// <summary>
/// Lists tickets. When <paramref name="RequestingCustomerUserId"/> is set (customer endpoint),
/// results are scoped to that customer's own tickets regardless of the other filters; otherwise
/// (admin endpoint) all tickets are visible, filterable/sortable/searchable.
/// </summary>
public sealed record GetTicketsQuery(
    string? RequestingCustomerUserId,
    int Page,
    int PageSize,
    string? Search,
    TicketStatus? Status,
    Guid? CategoryId,
    Guid? PriorityId,
    string? AssignedAgentUserId,
    Guid? TagId,
    bool? Unassigned,
    string SortBy,
    bool SortDescending) : IRequest<Result<PagedResult<TicketSummaryDto>>>;
