using HAMBOX.Domain.Entities;

namespace HAMBOX.Modules.Support.Domain.Tickets;

public sealed class TicketCategory : AggregateRoot, IAuditable, ISoftDeletable
{
    private TicketCategory()
    {
    }

    private TicketCategory(Guid id, string name, string color, string icon)
        : base(id)
    {
        Name = name;
        Color = color;
        Icon = icon;
    }

    public string Name { get; private set; } = string.Empty;
    public string Color { get; private set; } = "#6366F1";
    public string Icon { get; private set; } = "pi pi-tag";
    public int SortOrder { get; private set; }
    public bool IsActive { get; private set; } = true;
    public bool IsDefault { get; private set; }

    public bool IsDeleted { get; private set; }
    public DateTimeOffset? DeletedOnUtc { get; private set; }
    public string? CreatedBy { get; set; }
    public string? ModifiedBy { get; set; }

    public static TicketCategory Create(string name, string color, string icon, int sortOrder, bool isDefault = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return new TicketCategory(Guid.NewGuid(), name.Trim(), color, icon) { SortOrder = sortOrder, IsDefault = isDefault };
    }

    public void Update(string name, string color, string icon, int sortOrder, bool isActive)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name.Trim();
        Color = color;
        Icon = icon;
        SortOrder = sortOrder;
        IsActive = isActive;
    }

    public void MarkAsDefault() => IsDefault = true;

    public void Delete()
    {
        IsDeleted = true;
        DeletedOnUtc = DateTimeOffset.UtcNow;
    }
}
