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
        var now = DateTimeOffset.UtcNow;
        var last24h = now.AddHours(-24);
        var previous48hStart = now.AddHours(-48);
        var last7Days = now.AddDays(-7);
        var last14Days = now.AddDays(-14).Date;

        var alertSeverities = SecuritySeverityFilter.AtOrAbove(SecurityEventSeverity.High);

        var openAlerts = await dbContext.SecurityEventLogs.AsNoTracking()
            .CountAsync(e => e.Status == SecurityEventStatus.Open && alertSeverities.Contains(e.Severity), cancellationToken);

        var failedLoginsLast24h = await dbContext.LoginHistory.AsNoTracking()
            .CountAsync(h => !h.IsSuccessful && h.CreatedOnUtc >= last24h, cancellationToken);

        var failedLoginsPrevious24h = await dbContext.LoginHistory.AsNoTracking()
            .CountAsync(h => !h.IsSuccessful && h.CreatedOnUtc >= previous48hStart && h.CreatedOnUtc < last24h, cancellationToken);

        var activeSessions = await dbContext.UserSessions.AsNoTracking()
            .CountAsync(s => s.EndedOnUtc == null, cancellationToken);

        var newDevicesLast7Days = await dbContext.TrustedDevices.AsNoTracking()
            .CountAsync(d => d.FirstSeenUtc >= last7Days, cancellationToken);

        var loginTrend = await dbContext.LoginHistory.AsNoTracking()
            .Where(h => h.CreatedOnUtc >= last14Days)
            .GroupBy(h => h.CreatedOnUtc.Date)
            .Select(g => new
            {
                Date = g.Key,
                Successful = g.Count(h => h.IsSuccessful),
                Failed = g.Count(h => !h.IsSuccessful),
            })
            .OrderBy(g => g.Date)
            .ToListAsync(cancellationToken);

        var loginTrendDtos = loginTrend
            .Select(g => new LoginTrendPointDto(DateOnly.FromDateTime(g.Date), g.Successful, g.Failed))
            .ToList();

        var topFailureCountriesRaw = await dbContext.LoginHistory.AsNoTracking()
            .Where(h => !h.IsSuccessful && h.CountryCode != null && h.CreatedOnUtc >= last7Days)
            .GroupBy(h => h.CountryCode)
            .Select(g => new { CountryCode = g.Key, FailedLogins = g.Count() })
            .OrderByDescending(g => g.FailedLogins)
            .Take(5)
            .ToListAsync(cancellationToken);

        var topFailureCountries = topFailureCountriesRaw
            .Select(g => new CountryFailureCountDto(g.CountryCode!, g.FailedLogins))
            .ToList();

        var alertEvents = await dbContext.SecurityEventLogs.AsNoTracking()
            .Where(e => e.Status == SecurityEventStatus.Open && alertSeverities.Contains(e.Severity))
            .OrderByDescending(e => e.OccurredOnUtc)
            .Take(5)
            .ToListAsync(cancellationToken);

        var userIds = alertEvents
            .SelectMany(e => new[] { e.ActorUserId, e.TargetUserId })
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

        var emailsByUserId = await dbContext.Users.AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.Email })
            .ToDictionaryAsync(u => u.Id, u => u.Email, cancellationToken);

        var alertPreviewDtos = alertEvents.Select(e => new SecurityEventDto(
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

        return Result.Success(new SecurityDashboardDto(
            openAlerts,
            failedLoginsLast24h,
            failedLoginsPrevious24h,
            activeSessions,
            newDevicesLast7Days,
            loginTrendDtos,
            topFailureCountries,
            alertPreviewDtos));
    }
}
