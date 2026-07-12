using HAMBOX.Domain.Entities;

namespace HAMBOX.Modules.Themes.Domain.Themes;

public sealed class ThemeSchedule : BaseEntity
{
    private ThemeSchedule()
    {
    }

    private ThemeSchedule(Guid id, Guid themeId, DateTime startsAtUtc, DateTime? endsAtUtc, string? recurrenceRule)
        : base(id)
    {
        ThemeId = themeId;
        StartsAtUtc = startsAtUtc;
        EndsAtUtc = endsAtUtc;
        RecurrenceRule = recurrenceRule;
        IsActive = true;
    }

    public Guid ThemeId { get; private set; }
    public DateTime StartsAtUtc { get; private set; }
    public DateTime? EndsAtUtc { get; private set; }
    public string? RecurrenceRule { get; private set; }
    public bool IsActive { get; private set; }

    public static ThemeSchedule Create(Guid themeId, DateTime startsAtUtc, DateTime? endsAtUtc, string? recurrenceRule)
    {
        if (endsAtUtc.HasValue && endsAtUtc <= startsAtUtc)
        {
            throw new ArgumentException("End must be after start.");
        }

        return new ThemeSchedule(Guid.NewGuid(), themeId, startsAtUtc, endsAtUtc, recurrenceRule);
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
