using HAMBOX.Modules.Identity.Application.Abstractions;
using HAMBOX.Modules.Support.Application.Abstractions;
using HAMBOX.Modules.Support.Application.Contracts;
using HAMBOX.Modules.Support.Application.Services;
using HAMBOX.Modules.Support.Domain.Tickets;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Support.Application.Features.Analytics.GetSupportStatistics;

internal sealed class GetSupportStatisticsQueryHandler(ISupportDbContext dbContext, IIdentityDbContext identityDb)
    : IRequestHandler<GetSupportStatisticsQuery, Result<SupportStatisticsDto>>
{
    public async Task<Result<SupportStatisticsDto>> Handle(GetSupportStatisticsQuery request, CancellationToken cancellationToken)
    {
        var dateFrom = request.DateFrom ?? DateTimeOffset.UtcNow.AddDays(-30);
        var dateTo = request.DateTo ?? DateTimeOffset.UtcNow;

        var tickets = await dbContext.Tickets
            .AsNoTracking()
            .Where(t => t.CreatedOnUtc >= dateFrom && t.CreatedOnUtc <= dateTo)
            .ToListAsync(cancellationToken);

        var totalTickets = tickets.Count;
        var openTickets = tickets.Count(t => t.Status == TicketStatus.Open);
        var waitingCustomer = tickets.Count(t => t.Status == TicketStatus.WaitingCustomer);
        var waitingAgent = tickets.Count(t => t.Status == TicketStatus.WaitingAgent);
        var resolvedTickets = tickets.Count(t => t.Status == TicketStatus.Resolved);
        var closedTickets = tickets.Count(t => t.Status == TicketStatus.Closed);

        var ticketsByDay = tickets
            .GroupBy(t => DateOnly.FromDateTime(t.CreatedOnUtc.UtcDateTime))
            .OrderBy(g => g.Key)
            .Select(g => new TicketVolumePointDto(g.Key, g.Count()))
            .ToList();

        var firstResponseSamples = tickets
            .Where(t => t.FirstResponseOnUtc is not null)
            .Select(t => (t.FirstResponseOnUtc!.Value - t.CreatedOnUtc).TotalMinutes)
            .ToList();
        var averageFirstResponseMinutes = firstResponseSamples.Count > 0 ? firstResponseSamples.Average() : (double?)null;

        var resolutionSamples = tickets
            .Where(t => t.ResolvedOnUtc is not null)
            .Select(t => (t.ResolvedOnUtc!.Value - t.CreatedOnUtc).TotalMinutes)
            .ToList();
        var averageResolutionMinutes = resolutionSamples.Count > 0 ? resolutionSamples.Average() : (double?)null;

        var openAssignedTickets = tickets
            .Where(t => t.AssignedAgentUserId is not null && t.Status is TicketStatus.Open or TicketStatus.WaitingAgent or TicketStatus.WaitingCustomer)
            .GroupBy(t => t.AssignedAgentUserId!)
            .Select(g => (AgentUserId: g.Key, Count: g.Count()))
            .ToList();

        var agentNames = await UserDisplayResolver.ResolveAsync(
            identityDb, openAssignedTickets.Select(a => a.AgentUserId), cancellationToken);

        var agentWorkload = openAssignedTickets
            .Select(a => new AgentWorkloadDto(a.AgentUserId, agentNames.GetValueOrDefault(a.AgentUserId)?.Name ?? "Unknown", a.Count))
            .OrderByDescending(a => a.OpenAssignedCount)
            .ToList();

        var categoryIds = tickets.Where(t => t.CategoryId is not null).Select(t => t.CategoryId!.Value).Distinct().ToList();
        var categories = await dbContext.TicketCategories.AsNoTracking()
            .Where(c => categoryIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.Name, cancellationToken);
        var categoryBreakdown = tickets
            .Where(t => t.CategoryId is not null)
            .GroupBy(t => t.CategoryId!.Value)
            .Select(g => new CategoryBreakdownDto(g.Key, categories.GetValueOrDefault(g.Key, "Unknown"), g.Count()))
            .OrderByDescending(c => c.Count)
            .ToList();

        var priorityIds = tickets.Where(t => t.PriorityId is not null).Select(t => t.PriorityId!.Value).Distinct().ToList();
        var priorities = await dbContext.TicketPriorities.AsNoTracking()
            .Where(p => priorityIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.Name, cancellationToken);
        var priorityBreakdown = tickets
            .Where(t => t.PriorityId is not null)
            .GroupBy(t => t.PriorityId!.Value)
            .Select(g => new PriorityBreakdownDto(g.Key, priorities.GetValueOrDefault(g.Key, "Unknown"), g.Count()))
            .OrderByDescending(p => p.Count)
            .ToList();

        var ratings = tickets.Where(t => t.RatingScore is not null).Select(t => (double)t.RatingScore!.Value).ToList();
        var averageRating = ratings.Count > 0 ? ratings.Average() : (double?)null;

        return Result.Success(new SupportStatisticsDto(
            totalTickets, openTickets, waitingCustomer, waitingAgent, resolvedTickets, closedTickets,
            ticketsByDay, averageFirstResponseMinutes, averageResolutionMinutes,
            agentWorkload, categoryBreakdown, priorityBreakdown, averageRating, ratings.Count));
    }
}
