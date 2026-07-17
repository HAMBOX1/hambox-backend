using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Communication.Application.Contracts;
using HAMBOX.Modules.Communication.Application.Abstractions;
using HAMBOX.Modules.Communication.Domain.Communication;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Communication.Application.Features.Dashboard;

public sealed record GetCommunicationDashboardStatsQuery : IRequest<Result<CommunicationDashboardStatsDto>>;

internal sealed class GetCommunicationDashboardStatsQueryHandler(
    ICommunicationDbContext db,
    ICommerceDbContext commerceDb)
    : IRequestHandler<GetCommunicationDashboardStatsQuery, Result<CommunicationDashboardStatsDto>>
{
    public async Task<Result<CommunicationDashboardStatsDto>> Handle(GetCommunicationDashboardStatsQuery request, CancellationToken cancellationToken)
    {
        var todayStartUtc = DateTimeOffset.UtcNow.Date;

        var todayMessages = await db.CommunicationMessages.AsNoTracking()
            .Where(m => m.CreatedOnUtc >= todayStartUtc)
            .Select(m => new { m.Channel, m.Status, m.CreatedOnUtc, m.SentOnUtc })
            .ToListAsync(cancellationToken);

        var notificationsToday = todayMessages.Count;
        var emailsSentToday = todayMessages.Count(m => m.Channel == CommunicationChannels.Email
            && (m.Status == CommunicationStatus.Sent || m.Status == CommunicationStatus.Delivered));
        var failuresToday = todayMessages.Count(m => m.Status == CommunicationStatus.Failed);

        var deliveredDurations = todayMessages
            .Where(m => m.SentOnUtc.HasValue)
            .Select(m => (m.SentOnUtc!.Value - m.CreatedOnUtc).TotalSeconds)
            .ToList();
        var averageDeliverySeconds = deliveredDurations.Count > 0 ? deliveredDurations.Average() : (double?)null;

        var retryingCount = await db.CommunicationMessages.AsNoTracking()
            .CountAsync(m => m.Status == CommunicationStatus.Retrying, cancellationToken);

        var queueSize = await db.CommunicationMessages.AsNoTracking()
            .CountAsync(m => m.Status == CommunicationStatus.Queued || m.Status == CommunicationStatus.Retrying, cancellationToken);

        var unreadNotifications = await commerceDb.UserNotifications.AsNoTracking()
            .CountAsync(n => !n.IsRead, cancellationToken);

        var providers = await db.CommunicationProviderConfigs.AsNoTracking()
            .Select(p => new CommunicationProviderStatusDto(p.Channel, p.ProviderType, p.IsEnabled))
            .ToListAsync(cancellationToken);

        return Result.Success(new CommunicationDashboardStatsDto(
            notificationsToday,
            emailsSentToday,
            failuresToday,
            retryingCount,
            unreadNotifications,
            averageDeliverySeconds,
            queueSize,
            providers));
    }
}
