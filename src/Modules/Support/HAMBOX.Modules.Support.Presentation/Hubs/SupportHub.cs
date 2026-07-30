using HAMBOX.Modules.Identity.Application.Authorization;
using HAMBOX.Modules.Support.Application.Features.Tickets.MarkTicketMessageRead;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace HAMBOX.Modules.Support.Presentation.Hubs;

/// <summary>
/// Real-time channel for the Support module — genuinely greenfield (no SignalR anywhere else
/// in the solution). Groups: <c>ticket-{id}</c> (everyone currently viewing a ticket),
/// <c>user-{userId}</c> (a user's personal channel, for events not tied to one open ticket),
/// <c>support-agents</c> (every connected staff member, for "new ticket" inbox updates).
/// </summary>
[Authorize]
public sealed class SupportHub(ISender sender) : Hub
{
    public const string AgentsGroup = "support-agents";

    public static string TicketGroup(Guid ticketId) => $"ticket-{ticketId}";

    /// <summary>Staff-only sub-group for a ticket — internal notes broadcast here only, never
    /// to <see cref="TicketGroup"/>, since a customer connection may also hold that group.</summary>
    public static string TicketStaffGroup(Guid ticketId) => $"ticket-{ticketId}-staff";

    public static string UserGroup(string userId) => $"user-{userId}";

    public override async Task OnConnectedAsync()
    {
        var userId = Context.UserIdentifier;
        if (!string.IsNullOrWhiteSpace(userId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, UserGroup(userId));
        }

        if (IsStaff())
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, AgentsGroup);
            await Clients.Group(AgentsGroup).SendAsync("PresenceChanged", userId, true);
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (IsStaff())
        {
            await Clients.Group(AgentsGroup).SendAsync("PresenceChanged", Context.UserIdentifier, false);
        }

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>Join a ticket's live channel. Ownership/permission was already enforced by the
    /// REST call that fetched the ticket the client is now viewing — the hub trusts an
    /// authenticated connection the same way the endpoint layer trusts a valid permission claim.</summary>
    public async Task JoinTicket(Guid ticketId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, TicketGroup(ticketId));
        if (IsStaff())
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, TicketStaffGroup(ticketId));
        }
    }

    public async Task LeaveTicket(Guid ticketId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, TicketGroup(ticketId));
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, TicketStaffGroup(ticketId));
    }

    public async Task Typing(Guid ticketId) =>
        await Clients.OthersInGroup(TicketGroup(ticketId)).SendAsync("TypingIndicator", ticketId, Context.UserIdentifier, true);

    public async Task StopTyping(Guid ticketId) =>
        await Clients.OthersInGroup(TicketGroup(ticketId)).SendAsync("TypingIndicator", ticketId, Context.UserIdentifier, false);

    public async Task MarkMessageRead(Guid ticketId, Guid messageId)
    {
        var userId = Context.UserIdentifier;
        if (string.IsNullOrWhiteSpace(userId))
        {
            return;
        }

        await sender.Send(new MarkTicketMessageReadCommand(ticketId, messageId, userId));
    }

    private bool IsStaff() => Context.User?.HasClaim(IdentityClaimTypes.AuthContext, AuthContextTypes.Admin) == true;
}
