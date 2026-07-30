using HAMBOX.Modules.Identity.Application.Abstractions;
using HAMBOX.Modules.Support.Application.Abstractions;
using HAMBOX.Modules.Support.Application.Contracts;
using HAMBOX.Modules.Support.Application.Services;
using HAMBOX.Modules.Support.Domain.Tickets;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Support.Application.Features.Tickets.GetTickets;

internal sealed class GetTicketsQueryHandler(ISupportDbContext dbContext, IIdentityDbContext identityDb)
    : IRequestHandler<GetTicketsQuery, Result<PagedResult<TicketSummaryDto>>>
{
    public async Task<Result<PagedResult<TicketSummaryDto>>> Handle(GetTicketsQuery request, CancellationToken cancellationToken)
    {
        var query = dbContext.Tickets.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.RequestingCustomerUserId))
        {
            query = query.Where(t => t.CustomerUserId == request.RequestingCustomerUserId);
        }

        if (request.Status is not null)
        {
            query = query.Where(t => t.Status == request.Status);
        }

        if (request.CategoryId is not null)
        {
            query = query.Where(t => t.CategoryId == request.CategoryId);
        }

        if (request.PriorityId is not null)
        {
            query = query.Where(t => t.PriorityId == request.PriorityId);
        }

        if (!string.IsNullOrWhiteSpace(request.AssignedAgentUserId))
        {
            query = query.Where(t => t.AssignedAgentUserId == request.AssignedAgentUserId);
        }

        if (request.Unassigned == true)
        {
            query = query.Where(t => t.AssignedAgentUserId == null);
        }

        if (request.TagId is Guid tagId)
        {
            var taggedTicketIds = dbContext.TicketTagAssignments.Where(a => a.TagId == tagId).Select(a => a.TicketId);
            query = query.Where(t => taggedTicketIds.Contains(t.Id));
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            query = query.Where(t => t.Subject.Contains(term) || t.TicketNumber.Contains(term));
        }

        query = (request.SortBy?.ToLowerInvariant(), request.SortDescending) switch
        {
            ("subject", true) => query.OrderByDescending(t => t.Subject),
            ("subject", false) => query.OrderBy(t => t.Subject),
            ("status", true) => query.OrderByDescending(t => t.Status),
            ("status", false) => query.OrderBy(t => t.Status),
            ("lastmessage", false) => query.OrderBy(t => t.LastMessageOnUtc),
            ("lastmessage", true) => query.OrderByDescending(t => t.LastMessageOnUtc),
            (_, false) => query.OrderBy(t => t.CreatedOnUtc),
            _ => query.OrderByDescending(t => t.CreatedOnUtc),
        };

        var totalCount = await query.CountAsync(cancellationToken);

        var tickets = await query
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var ticketIds = tickets.Select(t => t.Id).ToList();
        var categoryIds = tickets.Where(t => t.CategoryId is not null).Select(t => t.CategoryId!.Value).Distinct().ToList();
        var priorityIds = tickets.Where(t => t.PriorityId is not null).Select(t => t.PriorityId!.Value).Distinct().ToList();

        var categories = await dbContext.TicketCategories.AsNoTracking()
            .Where(c => categoryIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, cancellationToken);
        var priorities = await dbContext.TicketPriorities.AsNoTracking()
            .Where(p => priorityIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, cancellationToken);

        var tagAssignments = await dbContext.TicketTagAssignments.AsNoTracking()
            .Where(a => ticketIds.Contains(a.TicketId)).ToListAsync(cancellationToken);
        var allTagIds = tagAssignments.Select(a => a.TagId).Distinct().ToList();
        var tagsById = await dbContext.TicketTags.AsNoTracking()
            .Where(t => allTagIds.Contains(t.Id)).ToDictionaryAsync(t => t.Id, cancellationToken);

        var userIds = tickets.SelectMany(t => new[] { t.CustomerUserId, t.AssignedAgentUserId });
        var users = await UserDisplayResolver.ResolveAsync(identityDb, userIds, cancellationToken);

        var summaries = tickets.Select(ticket =>
        {
            categories.TryGetValue(ticket.CategoryId ?? Guid.Empty, out var category);
            priorities.TryGetValue(ticket.PriorityId ?? Guid.Empty, out var priority);
            users.TryGetValue(ticket.CustomerUserId, out var customer);
            var agentName = ticket.AssignedAgentUserId is not null && users.TryGetValue(ticket.AssignedAgentUserId, out var agent)
                ? agent.Name
                : null;

            var tags = tagAssignments
                .Where(a => a.TicketId == ticket.Id)
                .Select(a => tagsById.TryGetValue(a.TagId, out var tag) ? SupportMapper.ToDto(tag) : null)
                .Where(dto => dto is not null)
                .Select(dto => dto!)
                .ToList();

            return SupportMapper.ToSummaryDto(
                ticket, category, priority, customer?.Name ?? "Unknown", customer?.Email ?? string.Empty, agentName, tags);
        }).ToList();

        return Result.Success(new PagedResult<TicketSummaryDto>(summaries, request.Page, request.PageSize, totalCount));
    }
}
