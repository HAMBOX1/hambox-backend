using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace HAMBOX.Modules.Commerce.Presentation.Hubs;

/// <summary>
/// Real-time channel for a user's own in-app notifications (bell + Notification Center). Single
/// group per user (<c>user-{userId}</c>), mirroring Support's <c>SupportHub</c> convention.
/// <c>Context.UserIdentifier</c> is resolved by the app-wide <c>IUserIdProvider</c> already
/// registered for the Support hub — SignalR only has one slot for that service across every hub
/// in the app, not one per hub, so no second registration is needed here.
/// </summary>
[Authorize]
public sealed class NotificationHub : Hub
{
    public static string UserGroup(string userId) => $"user-{userId}";

    public override async Task OnConnectedAsync()
    {
        var userId = Context.UserIdentifier;
        if (!string.IsNullOrWhiteSpace(userId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, UserGroup(userId));
        }

        await base.OnConnectedAsync();
    }
}
