using HAMBOX.Domain.Entities;

namespace HAMBOX.Modules.Support.Domain.Tickets;

public sealed class TicketPriority : AggregateRoot, IAuditable, ISoftDeletable
{
    private TicketPriority()
    {
    }

    private TicketPriority(Guid id, string name, string color, int level)
        : base(id)
    {
        Name = name;
        Color = color;
        Level = level;
    }

    public string Name { get; private set; } = string.Empty;
    public string Color { get; private set; } = "#6366F1";

    /// <summary>Higher is more urgent — drives sort order and escalation comparisons.</summary>
    public int Level { get; private set; }
    public bool IsActive { get; private set; } = true;
    public bool IsDefault { get; private set; }

    /// <summary>SLA targets in minutes; null means no SLA tracked for this priority.</summary>
    public int? SlaFirstResponseMinutes { get; private set; }
    public int? SlaResolutionMinutes { get; private set; }

    public bool IsDeleted { get; private set; }
    public DateTimeOffset? DeletedOnUtc { get; private set; }
    public string? CreatedBy { get; set; }
    public string? ModifiedBy { get; set; }

    public static TicketPriority Create(
        string name, string color, int level, int? slaFirstResponseMinutes, int? slaResolutionMinutes, bool isDefault = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return new TicketPriority(Guid.NewGuid(), name.Trim(), color, level)
        {
            SlaFirstResponseMinutes = slaFirstResponseMinutes,
            SlaResolutionMinutes = slaResolutionMinutes,
            IsDefault = isDefault,
        };
    }

    public void Update(string name, string color, int level, int? slaFirstResponseMinutes, int? slaResolutionMinutes, bool isActive)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name.Trim();
        Color = color;
        Level = level;
        SlaFirstResponseMinutes = slaFirstResponseMinutes;
        SlaResolutionMinutes = slaResolutionMinutes;
        IsActive = isActive;
    }

    public void MarkAsDefault() => IsDefault = true;

    public void Delete()
    {
        IsDeleted = true;
        DeletedOnUtc = DateTimeOffset.UtcNow;
    }
}
