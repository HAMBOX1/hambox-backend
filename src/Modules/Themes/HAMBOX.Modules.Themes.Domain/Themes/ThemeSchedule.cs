using HAMBOX.Domain.Entities;

namespace HAMBOX.Modules.Themes.Domain.Themes;

public sealed class ThemeSchedule : BaseEntity
{
    private ThemeSchedule()
    {
    }

    private ThemeSchedule(Guid id, Guid themeId, DateTime startsAtUtc, DateTime? endsAtUtc, string? recurrenceRule, int priority)
        : base(id)
    {
        ThemeId = themeId;
        StartsAtUtc = startsAtUtc;
        EndsAtUtc = endsAtUtc;
        RecurrenceRule = recurrenceRule;
        Priority = priority;
        IsActive = true;
    }

    public Guid ThemeId { get; private set; }
    public DateTime StartsAtUtc { get; private set; }
    public DateTime? EndsAtUtc { get; private set; }
    public string? RecurrenceRule { get; private set; }

    /// <summary>
    /// Tiebreak for overlapping schedules across different themes — mirrors <see cref="ThemeAssignment.Priority"/>.
    /// Higher wins; falls back to the later start time when priorities match.
    /// </summary>
    public int Priority { get; private set; }
    public bool IsActive { get; private set; }

    public static ThemeSchedule Create(Guid themeId, DateTime startsAtUtc, DateTime? endsAtUtc, string? recurrenceRule, int priority = 0)
    {
        // The resolver compares these against DateTime.UtcNow directly, so a non-UTC value would
        // silently activate/deactivate the schedule hours off from what the admin intended.
        if (startsAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("StartsAtUtc must be a UTC DateTime.", nameof(startsAtUtc));
        }

        if (endsAtUtc.HasValue && endsAtUtc.Value.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("EndsAtUtc must be a UTC DateTime.", nameof(endsAtUtc));
        }

        if (endsAtUtc.HasValue && endsAtUtc <= startsAtUtc)
        {
            throw new ArgumentException("End must be after start.");
        }

        return new ThemeSchedule(Guid.NewGuid(), themeId, startsAtUtc, endsAtUtc, recurrenceRule, priority);
    }

    public bool IsEffectiveAt(DateTime utcNow)
    {
        if (!IsActive)
        {
            return false;
        }

        if (utcNow < StartsAtUtc)
        {
            return false;
        }

        return !EndsAtUtc.HasValue || utcNow <= EndsAtUtc.Value;
    }

    public void Deactivate() => IsActive = false;
}
