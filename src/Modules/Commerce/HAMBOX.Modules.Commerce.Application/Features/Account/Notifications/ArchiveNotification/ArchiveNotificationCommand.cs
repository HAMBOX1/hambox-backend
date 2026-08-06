using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Errors;
using HAMBOX.Modules.Commerce.Application.Services;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Commerce.Application.Features.Account.Notifications.ArchiveNotification;

public sealed record ArchiveNotificationCommand(Guid NotificationId) : IRequest<Result>;

internal sealed class ArchiveNotificationCommandHandler(
    ICommerceDbContext commerceDbContext,
    ICurrentUserService currentUserService,
    IUserNotificationRealtimeNotifier realtimeNotifier) : IRequestHandler<ArchiveNotificationCommand, Result>
{
    public async Task<Result> Handle(ArchiveNotificationCommand request, CancellationToken cancellationToken)
    {
        if (!currentUserService.IsAuthenticated || currentUserService.UserId is null)
        {
            return Result.Failure(CommerceErrors.AuthenticationRequired);
        }

        var notification = await commerceDbContext.UserNotifications
            .FirstOrDefaultAsync(n => n.Id == request.NotificationId && n.UserId == currentUserService.UserId, cancellationToken);

        if (notification is null)
        {
            return Result.Failure(CommerceErrors.NotificationNotFound);
        }

        notification.Archive();
        await commerceDbContext.SaveChangesAsync(cancellationToken);

        var unreadCount = await commerceDbContext.UserNotifications
            .CountAsync(n => n.UserId == currentUserService.UserId && !n.IsRead, cancellationToken);
        await realtimeNotifier.NotifyNotificationUpdatedAsync(
            currentUserService.UserId, AccountMapper.ToUserNotificationDto(notification), unreadCount, cancellationToken);

        return Result.Success();
    }
}
