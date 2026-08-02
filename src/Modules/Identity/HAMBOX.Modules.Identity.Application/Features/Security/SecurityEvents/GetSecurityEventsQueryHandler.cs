using HAMBOX.Modules.Identity.Application.Abstractions;
using HAMBOX.Modules.Identity.Application.Contracts;
using HAMBOX.Modules.Identity.Domain.Enums;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Identity.Application.Features.Security.SecurityEvents;

internal sealed class GetSecurityEventsQueryHandler(IIdentityDbContext dbContext)
    : IRequestHandler<GetSecurityEventsQuery, Result<PagedResult<SecurityEventDto>>>
{
    public async Task<Result<PagedResult<SecurityEventDto>>> Handle(
        GetSecurityEventsQuery request,
        CancellationToken cancellationToken)
    {
        var query = dbContext.SecurityEventLogs.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.EventType) &&
            Enum.TryParse<SecurityEventType>(request.EventType, ignoreCase: true, out var eventType))
        {
            query = query.Where(e => e.EventType == eventType);
        }

        if (!string.IsNullOrWhiteSpace(request.Severity) &&
            Enum.TryParse<SecurityEventSeverity>(request.Severity, ignoreCase: true, out var severity))
        {
            query = query.Where(e => e.Severity == severity);
        }

        if (request.FromUtc.HasValue)
        {
            query = query.Where(e => e.OccurredOnUtc >= request.FromUtc.Value);
        }

        if (request.ToUtc.HasValue)
        {
            query = query.Where(e => e.OccurredOnUtc <= request.ToUtc.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim();
            query = query.Where(e =>
                e.Description.Contains(term) ||
                (e.IpAddress != null && e.IpAddress.Contains(term)));
        }

        if (!string.IsNullOrWhiteSpace(request.Status) &&
            Enum.TryParse<SecurityEventStatus>(request.Status, ignoreCase: true, out var status))
        {
            query = query.Where(e => e.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(request.MinSeverity) &&
            Enum.TryParse<SecurityEventSeverity>(request.MinSeverity, ignoreCase: true, out var minSeverity))
        {
            var severities = SecuritySeverityFilter.AtOrAbove(minSeverity);
            query = query.Where(e => severities.Contains(e.Severity));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var events = await query
            .OrderByDescending(e => e.OccurredOnUtc)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var userIds = events
            .SelectMany(e => new[] { e.ActorUserId, e.TargetUserId })
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

        var emailsByUserId = await dbContext.Users
            .AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.Email })
            .ToDictionaryAsync(u => u.Id, u => u.Email, cancellationToken);

        var items = events.Select(e => new SecurityEventDto(
            e.Id,
            e.EventType.ToString(),
            e.Severity.ToString(),
            e.Description,
            e.ActorUserId,
            e.ActorUserId.HasValue ? emailsByUserId.GetValueOrDefault(e.ActorUserId.Value) : null,
            e.TargetUserId,
            e.TargetUserId.HasValue ? emailsByUserId.GetValueOrDefault(e.TargetUserId.Value) : null,
            e.IpAddress,
            e.Country,
            e.City,
            e.UserAgent,
            e.CorrelationId,
            e.OccurredOnUtc,
            e.Status.ToString(),
            e.AcknowledgedByUserId,
            e.AcknowledgedOnUtc,
            e.ResolvedByUserId,
            e.ResolvedOnUtc,
            e.ResolutionNotes)).ToList();

        return Result.Success(new PagedResult<SecurityEventDto>(items, request.PageNumber, request.PageSize, totalCount));
    }
}
