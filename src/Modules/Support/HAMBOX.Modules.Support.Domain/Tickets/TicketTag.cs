using HAMBOX.Domain.Entities;

namespace HAMBOX.Modules.Support.Domain.Tickets;

public sealed class TicketTag : AggregateRoot, IAuditable, ISoftDeletable
{
    private TicketTag()
    {
    }

    private TicketTag(Guid id, string name, string color)
        : base(id)
    {
        Name = name;
        Color = color;
    }

    public string Name { get; private set; } = string.Empty;
    public string Color { get; private set; } = "#6366F1";

    public bool IsDeleted { get; private set; }
    public DateTimeOffset? DeletedOnUtc { get; private set; }
    public string? CreatedBy { get; set; }
    public string? ModifiedBy { get; set; }

    public static TicketTag Create(string name, string color)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(color);
        return new TicketTag(Guid.NewGuid(), name.Trim(), color);
    }

    public void Update(string name, string color)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(color);
        Name = name.Trim();
        Color = color;
    }

    public void Delete()
    {
        IsDeleted = true;
        DeletedOnUtc = DateTimeOffset.UtcNow;
    }
}

public sealed class TicketTagAssignment : Entity
{
    private TicketTagAssignment()
    {
    }

    private TicketTagAssignment(Guid id, Guid ticketId, Guid tagId)
        : base(id)
    {
        TicketId = ticketId;
        TagId = tagId;
    }

    public Guid TicketId { get; private set; }
    public Guid TagId { get; private set; }

    public static TicketTagAssignment Create(Guid ticketId, Guid tagId) => new(Guid.NewGuid(), ticketId, tagId);
}
