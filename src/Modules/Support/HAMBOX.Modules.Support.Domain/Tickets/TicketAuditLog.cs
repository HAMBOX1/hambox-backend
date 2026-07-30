using HAMBOX.Domain.Entities;

namespace HAMBOX.Modules.Support.Domain.Tickets;

/// <summary>Dedicated audit entity for the Support module, following the per-module audit-log
/// convention used by Platform Settings/Inventory/Themes rather than a shared abstraction.</summary>
public sealed class TicketAuditLog : Entity
{
    private TicketAuditLog()
    {
    }

    private TicketAuditLog(Guid id, Guid ticketId, TicketAuditAction action, string? actorUserId, string? detailsJson)
        : base(id)
    {
        TicketId = ticketId;
        Action = action;
        ActorUserId = actorUserId;
        DetailsJson = detailsJson;
    }

    public Guid TicketId { get; private set; }
    public TicketAuditAction Action { get; private set; }
    public string? ActorUserId { get; private set; }
    public string? DetailsJson { get; private set; }

    public static TicketAuditLog Create(Guid ticketId, TicketAuditAction action, string? actorUserId, string? detailsJson = null) =>
        new(Guid.NewGuid(), ticketId, action, actorUserId, detailsJson);
}
