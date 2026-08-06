using HAMBOX.Modules.Commerce.Application.Contracts.Account;

namespace HAMBOX.Modules.Commerce.Application.Abstractions;

/// <summary>
/// Push channel for a user's in-app notification state (created/updated/deleted/bulk-read), so the
/// notification bell and Notification Center update live without polling. Mirrors
/// <c>ISupportRealtimeNotifier</c> — implemented over SignalR in the Presentation layer. Persistence
/// (<c>commerce.UserNotifications</c>) and the REST endpoints are unaffected by this interface; it only
/// announces state that was already committed.
/// </summary>
public interface IUserNotificationRealtimeNotifier
{
    Task NotifyNotificationCreatedAsync(
        string userId, UserNotificationDto notification, int unreadCount, CancellationToken cancellationToken = default);

    Task NotifyNotificationUpdatedAsync(
        string userId, UserNotificationDto notification, int unreadCount, CancellationToken cancellationToken = default);

    Task NotifyNotificationDeletedAsync(
        string userId, Guid notificationId, int unreadCount, CancellationToken cancellationToken = default);

    Task NotifyAllNotificationsReadAsync(string userId, CancellationToken cancellationToken = default);
}
