using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Contracts.Account;
using Microsoft.AspNetCore.SignalR;

namespace HAMBOX.Modules.Commerce.Presentation.Hubs;

internal sealed class SignalRUserNotificationRealtimeNotifier(IHubContext<NotificationHub> hubContext)
    : IUserNotificationRealtimeNotifier
{
    public Task NotifyNotificationCreatedAsync(
        string userId, UserNotificationDto notification, int unreadCount, CancellationToken cancellationToken = default) =>
        hubContext.Clients.Group(NotificationHub.UserGroup(userId))
            .SendAsync("NotificationCreated", notification, unreadCount, cancellationToken);

    public Task NotifyNotificationUpdatedAsync(
        string userId, UserNotificationDto notification, int unreadCount, CancellationToken cancellationToken = default) =>
        hubContext.Clients.Group(NotificationHub.UserGroup(userId))
            .SendAsync("NotificationUpdated", notification, unreadCount, cancellationToken);

    public Task NotifyNotificationDeletedAsync(
        string userId, Guid notificationId, int unreadCount, CancellationToken cancellationToken = default) =>
        hubContext.Clients.Group(NotificationHub.UserGroup(userId))
            .SendAsync("NotificationDeleted", notificationId, unreadCount, cancellationToken);

    public Task NotifyAllNotificationsReadAsync(string userId, CancellationToken cancellationToken = default) =>
        hubContext.Clients.Group(NotificationHub.UserGroup(userId))
            .SendAsync("AllNotificationsRead", cancellationToken);
}
