using HAMBOX.Domain.Entities;

namespace HAMBOX.Modules.Support.Domain.Tickets;

public enum TicketParticipantRole
{
    Owner = 0,
    Watcher = 1,
    Collaborator = 2,
}

/// <summary>
/// A user (customer or agent) attached to a ticket beyond the primary customer/assigned-agent
/// pair — CC'd agents, watchers who want notifications, transferred-from agents kept as
/// collaborators, etc.
/// </summary>
public sealed class TicketParticipant : Entity
{
    private TicketParticipant()
    {
    }

    private TicketParticipant(Guid id, Guid ticketId, string userId, TicketParticipantRole role)
        : base(id)
    {
        TicketId = ticketId;
        UserId = userId;
        Role = role;
    }

    public Guid TicketId { get; private set; }
    public string UserId { get; private set; } = string.Empty;
    public TicketParticipantRole Role { get; private set; }

    public static TicketParticipant Create(Guid ticketId, string userId, TicketParticipantRole role)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        return new TicketParticipant(Guid.NewGuid(), ticketId, userId, role);
    }
}
