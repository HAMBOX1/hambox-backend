using HAMBOX.Domain.Entities;

namespace HAMBOX.Modules.Support.Domain.Tickets;

public sealed class TicketStatusHistory : Entity
{
    private TicketStatusHistory()
    {
    }

    private TicketStatusHistory(Guid id, Guid ticketId, TicketStatus fromStatus, TicketStatus toStatus, string changedByUserId)
        : base(id)
    {
        TicketId = ticketId;
        FromStatus = fromStatus;
        ToStatus = toStatus;
        ChangedByUserId = changedByUserId;
    }

    public Guid TicketId { get; private set; }
    public TicketStatus FromStatus { get; private set; }
    public TicketStatus ToStatus { get; private set; }
    public string ChangedByUserId { get; private set; } = string.Empty;

    public static TicketStatusHistory Create(Guid ticketId, TicketStatus fromStatus, TicketStatus toStatus, string changedByUserId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(changedByUserId);
        return new TicketStatusHistory(Guid.NewGuid(), ticketId, fromStatus, toStatus, changedByUserId);
    }
}
