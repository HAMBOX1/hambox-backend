using HAMBOX.Modules.Support.Application.Abstractions;
using HAMBOX.Modules.Support.Application.Contracts;
using HAMBOX.Modules.Support.Domain.Tickets;
using Microsoft.AspNetCore.SignalR;

namespace HAMBOX.Modules.Support.Presentation.Hubs;

internal sealed class SignalRSupportRealtimeNotifier(IHubContext<SupportHub> hubContext) : ISupportRealtimeNotifier
{
    public async Task NotifyTicketCreatedAsync(Guid ticketId, string customerUserId, CancellationToken cancellationToken = default)
    {
        await hubContext.Clients.Group(SupportHub.AgentsGroup)
            .SendAsync("TicketCreated", ticketId, cancellationToken);
        await hubContext.Clients.Group(SupportHub.UserGroup(customerUserId))
            .SendAsync("TicketCreated", ticketId, cancellationToken);
    }

    public async Task NotifyNewMessageAsync(Guid ticketId, TicketMessageDto message, CancellationToken cancellationToken = default)
    {
        var group = message.IsInternal ? SupportHub.TicketStaffGroup(ticketId) : SupportHub.TicketGroup(ticketId);
        await hubContext.Clients.Group(group).SendAsync("ReceiveMessage", ticketId, message, cancellationToken);
    }

    public async Task NotifyTicketUpdatedAsync(
        Guid ticketId, string customerUserId, string? assignedAgentUserId, TicketStatus status,
        CancellationToken cancellationToken = default)
    {
        var groups = new List<string> { SupportHub.TicketGroup(ticketId), SupportHub.UserGroup(customerUserId), SupportHub.AgentsGroup };
        if (!string.IsNullOrWhiteSpace(assignedAgentUserId))
        {
            groups.Add(SupportHub.UserGroup(assignedAgentUserId));
        }

        await hubContext.Clients.Groups(groups).SendAsync("TicketUpdated", ticketId, status, cancellationToken);
    }

    public async Task NotifyMessageStatusAsync(
        Guid ticketId, Guid messageId, bool isDelivered, bool isRead, CancellationToken cancellationToken = default)
    {
        await hubContext.Clients.Group(SupportHub.TicketGroup(ticketId))
            .SendAsync("MessageStatusChanged", ticketId, messageId, isDelivered, isRead, cancellationToken);
    }
}
