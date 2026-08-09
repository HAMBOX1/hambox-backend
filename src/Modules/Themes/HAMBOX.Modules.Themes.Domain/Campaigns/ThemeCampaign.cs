using HAMBOX.Domain.Entities;

namespace HAMBOX.Modules.Themes.Domain.Campaigns;

/// <summary>
/// A storefront-wide, time-boxed marketing theme override (Black Friday, Ramadan, Eid, ...).
/// A dedicated aggregate root, not a child of <see cref="Themes.StoreTheme"/> — it references a
/// theme but owns its own identity, lifecycle, and audit trail, since the admin UX needs a
/// cross-theme "all my campaigns" view rather than a per-theme sub-resource.
/// </summary>
public sealed class ThemeCampaign : AggregateRoot, ISoftDeletable
{
    private ThemeCampaign()
    {
    }

    private ThemeCampaign(
        Guid id,
        string name,
        string? description,
        Guid themeId,
        DateTime startsAtUtc,
        DateTime endsAtUtc,
        int priority)
        : base(id)
    {
        Name = name;
        Description = description;
        ThemeId = themeId;
        StartsAtUtc = startsAtUtc;
        EndsAtUtc = endsAtUtc;
        Priority = priority;
        Status = CampaignStatus.Draft;
        IsEnabled = true;
        IsDeleted = false;
    }

    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public Guid ThemeId { get; private set; }
    public DateTime StartsAtUtc { get; private set; }
    public DateTime EndsAtUtc { get; private set; }
    public int Priority { get; private set; }
    public CampaignStatus Status { get; private set; }
    public bool IsEnabled { get; private set; }
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedOnUtc { get; set; }

    public static ThemeCampaign Create(
        string name,
        string? description,
        Guid themeId,
        DateTime startsAtUtc,
        DateTime endsAtUtc,
        int priority = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ValidateWindow(startsAtUtc, endsAtUtc);

        return new ThemeCampaign(Guid.NewGuid(), name.Trim(), description?.Trim(), themeId, startsAtUtc, endsAtUtc, priority);
    }

    public void UpdateDetails(string name, string? description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name.Trim();
        Description = description?.Trim();
    }

    public void ChangeTheme(Guid themeId) => ThemeId = themeId;

    public void Reschedule(DateTime startsAtUtc, DateTime endsAtUtc, int priority)
    {
        ValidateWindow(startsAtUtc, endsAtUtc);
        StartsAtUtc = startsAtUtc;
        EndsAtUtc = endsAtUtc;
        Priority = priority;
    }

    /// <summary>
    /// Transitions Draft -> Published. This aggregate has no visibility into StoreTheme's status —
    /// whether the referenced theme is actually eligible (Published, not archived/deleted) is an
    /// external check the caller must perform first. See PublishCampaignCommandHandler, which is
    /// where "cannot publish while pointing to a Draft theme" is actually enforced.
    /// </summary>
    public void Publish() => Status = CampaignStatus.Published;

    public void Enable() => IsEnabled = true;

    public void Disable() => IsEnabled = false;

    public void Archive() => Status = CampaignStatus.Archived;

    /// <summary>
    /// Whether this campaign resolves on the storefront right now. Mirrors the condition
    /// ThemeEngine expresses directly in a LINQ query for EF translation — kept here too as the
    /// single source of truth for anything evaluating an already-loaded instance in memory
    /// (e.g. building admin DTOs), so the rule is defined once even though it's expressed twice.
    /// </summary>
    public bool IsEffectiveAt(DateTime utcNow) =>
        Status == CampaignStatus.Published && IsEnabled && utcNow >= StartsAtUtc && utcNow < EndsAtUtc;

    /// <summary>
    /// Computed, display-only phase for the admin UI. Never persisted, never consulted by the
    /// resolver — purely so the admin API/UI don't reimplement this comparison themselves.
    /// </summary>
    public CampaignPhase GetPhase(DateTime utcNow)
    {
        if (Status == CampaignStatus.Draft)
        {
            return CampaignPhase.Draft;
        }

        if (Status == CampaignStatus.Archived)
        {
            return CampaignPhase.Archived;
        }

        if (!IsEnabled)
        {
            return CampaignPhase.Paused;
        }

        if (utcNow < StartsAtUtc)
        {
            return CampaignPhase.Scheduled;
        }

        return utcNow < EndsAtUtc ? CampaignPhase.Active : CampaignPhase.Ended;
    }

    private static void ValidateWindow(DateTime startsAtUtc, DateTime endsAtUtc)
    {
        // The resolver compares these directly against DateTime.UtcNow — a non-UTC value would
        // silently activate/deactivate the campaign hours off from intent (same guard as
        // ThemeSchedule.Create from the Phase 1 hardening pass).
        if (startsAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("StartsAtUtc must be a UTC DateTime.", nameof(startsAtUtc));
        }

        if (endsAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("EndsAtUtc must be a UTC DateTime.", nameof(endsAtUtc));
        }

        if (endsAtUtc <= startsAtUtc)
        {
            throw new ArgumentException("EndsAtUtc must be after StartsAtUtc.");
        }
    }
}
