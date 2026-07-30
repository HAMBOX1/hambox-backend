using HAMBOX.Domain.Entities;

namespace HAMBOX.Modules.Support.Domain.Tickets;

public sealed class TicketMessage : Entity, IAuditable
{
    private TicketMessage()
    {
    }

    private TicketMessage(
        Guid id, Guid ticketId, string authorUserId, TicketMessageAuthorRole authorRole, string body, bool isInternal)
        : base(id)
    {
        TicketId = ticketId;
        AuthorUserId = authorUserId;
        AuthorRole = authorRole;
        Body = body;
        IsInternal = isInternal;
    }

    public Guid TicketId { get; private set; }
    public string AuthorUserId { get; private set; } = string.Empty;
    public TicketMessageAuthorRole AuthorRole { get; private set; }
    public string Body { get; private set; } = string.Empty;
    public bool IsInternal { get; private set; }
    public bool IsDelivered { get; private set; }
    public DateTimeOffset? DeliveredOnUtc { get; private set; }
    public bool IsRead { get; private set; }
    public DateTimeOffset? ReadOnUtc { get; private set; }
    public Guid? SavedReplyId { get; private set; }

    public string? CreatedBy { get; set; }
    public string? ModifiedBy { get; set; }

    public static TicketMessage Create(
        Guid ticketId, string authorUserId, TicketMessageAuthorRole authorRole, string body, bool isInternal, Guid? savedReplyId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(authorUserId);
        ArgumentException.ThrowIfNullOrWhiteSpace(body);

        return new TicketMessage(Guid.NewGuid(), ticketId, authorUserId, authorRole, body, isInternal)
        {
            SavedReplyId = savedReplyId,
        };
    }

    public void MarkDelivered()
    {
        if (IsDelivered)
        {
            return;
        }

        IsDelivered = true;
        DeliveredOnUtc = DateTimeOffset.UtcNow;
    }

    public void MarkRead()
    {
        if (IsRead)
        {
            return;
        }

        IsRead = true;
        ReadOnUtc = DateTimeOffset.UtcNow;
    }
}
