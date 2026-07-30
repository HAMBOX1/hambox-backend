using HAMBOX.Modules.Support.Application.Contracts;
using HAMBOX.Modules.Support.Domain.Tickets;

namespace HAMBOX.Modules.Support.Application.Abstractions;

/// <summary>
/// Broadcasts real-time ticket events. Implemented in Presentation (the only layer allowed to
/// know about SignalR/<c>IHubContext</c>) so Application stays framework-agnostic — the same
/// dependency direction as every other Application-layer abstraction with an outer-layer
/// implementation (<c>IFileStorage</c>, <c>ICommunicationService</c>).
/// </summary>
public interface ISupportRealtimeNotifier
{
    /// <summary>A ticket was created — pushed to the connected-agents channel (so any agent's
    /// inbox live-updates without needing a prior subscription) and the customer's own channel.</summary>
    Task NotifyTicketCreatedAsync(Guid ticketId, string customerUserId, CancellationToken cancellationToken = default);

    /// <summary>A new message was posted. When <see cref="TicketMessageDto.IsInternal"/> is set,
    /// this reaches only the ticket's staff sub-channel — never a customer connection.</summary>
    Task NotifyNewMessageAsync(Guid ticketId, TicketMessageDto message, CancellationToken cancellationToken = default);

    /// <summary>Ticket status/assignment/category/priority changed — pushed to viewers of the
    /// ticket and to the customer/agent's personal channel so list views update live.</summary>
    Task NotifyTicketUpdatedAsync(
        Guid ticketId, string customerUserId, string? assignedAgentUserId, TicketStatus status,
        CancellationToken cancellationToken = default);

    /// <summary>A message was marked delivered/read — pushed to everyone viewing the ticket.</summary>
    Task NotifyMessageStatusAsync(
        Guid ticketId, Guid messageId, bool isDelivered, bool isRead, CancellationToken cancellationToken = default);
}
