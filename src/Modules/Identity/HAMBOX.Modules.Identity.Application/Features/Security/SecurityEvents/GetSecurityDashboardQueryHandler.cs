using HAMBOX.Modules.Identity.Application.Abstractions;
using HAMBOX.Modules.Identity.Application.Contracts;
using HAMBOX.Modules.Identity.Domain.Enums;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Identity.Application.Features.Security.SecurityEvents;

internal sealed class GetSecurityDashboardQueryHandler(IIdentityDbContext dbContext)
    : IRequestHandler<GetSecurityDashboardQuery, Result<SecurityDashboardDto>>
{
    public async Task<Result<SecurityDashboardDto>> Handle(
        GetSecurityDashboardQuery request,
        CancellationToken cancellationToken)
    {
        var todayStartUtc = DateTimeOffset.UtcNow.Date;

        var blockedUsers = await dbContext.Users.AsNoTracking()
            .CountAsync(u => u.Status == UserStatus.Blocked || u.Status == UserStatus.Banned, cancellationToken);
        var suspendedUsers = await dbContext.Users.AsNoTracking()
            .CountAsync(u => u.Status == UserStatus.Suspended, cancellationToken);
        var blockedEmails = await dbContext.BlockedEmails.AsNoTracking()
            .CountAsync(b => !b.Pattern.StartsWith("*@"), cancellationToken);
        var blockedDomains = await dbContext.BlockedEmails.AsNoTracking()
            .CountAsync(b => b.Pattern.StartsWith("*@"), cancellationToken);
        var blockedCountries = await dbContext.CountryRestrictions.AsNoTracking()
            .CountAsync(c => c.Status != CountryRestrictionStatus.Allowed, cancellationToken);
        var blockedIps = await dbContext.BlockedIps.AsNoTracking().CountAsync(cancellationToken);
        var securityEventsToday = await dbContext.SecurityEventLogs.AsNoTracking()
            .CountAsync(e => e.OccurredOnUtc >= todayStartUtc, cancellationToken);
        var failedLoginsToday = await dbContext.SecurityEventLogs.AsNoTracking()
            .CountAsync(e => e.OccurredOnUtc >= todayStartUtc && e.EventType == SecurityEventType.FailedLogin, cancellationToken);

        var recentEvents = await dbContext.SecurityEventLogs.AsNoTracking()
            .OrderByDescending(e => e.OccurredOnUtc)
            .Take(10)
            .ToListAsync(cancellationToken);

        var userIds = recentEvents
            .SelectMany(e => new[] { e.ActorUserId, e.TargetUserId })
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

        var emailsByUserId = await dbContext.Users.AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.Email })
            .ToDictionaryAsync(u => u.Id, u => u.Email, cancellationToken);

        var recentEventDtos = recentEvents.Select(e => new SecurityEventDto(
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
            e.UserAgent,
            e.CorrelationId,
            e.OccurredOnUtc)).ToList();

        return Result.Success(new SecurityDashboardDto(
            blockedUsers,
            suspendedUsers,
            blockedEmails,
            blockedDomains,
            blockedCountries,
            blockedIps,
            securityEventsToday,
            failedLoginsToday,
            recentEventDtos));
    }
}
