using HAMBOX.Domain.Entities;

namespace HAMBOX.Modules.Support.Domain.Tickets;

/// <summary>
/// Historical log of every assign/transfer action on a ticket — distinct from
/// <see cref="Ticket.AssignedAgentUserId"/>, which only holds current state.
/// </summary>
public sealed class TicketAssignment : Entity
{
    private TicketAssignment()
    {
    }

    private TicketAssignment(
        Guid id, Guid ticketId, string? fromAgentUserId, string toAgentUserId, string assignedByUserId)
        : base(id)
    {
        TicketId = ticketId;
        FromAgentUserId = fromAgentUserId;
        ToAgentUserId = toAgentUserId;
        AssignedByUserId = assignedByUserId;
    }

    public Guid TicketId { get; private set; }
    public string? FromAgentUserId { get; private set; }
    public string ToAgentUserId { get; private set; } = string.Empty;
    public string AssignedByUserId { get; private set; } = string.Empty;

    public static TicketAssignment Create(Guid ticketId, string? fromAgentUserId, string toAgentUserId, string assignedByUserId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toAgentUserId);
        ArgumentException.ThrowIfNullOrWhiteSpace(assignedByUserId);
        return new TicketAssignment(Guid.NewGuid(), ticketId, fromAgentUserId, toAgentUserId, assignedByUserId);
    }
}
