namespace HAMBOX.Modules.Themes.Domain.Campaigns;

/// <summary>
/// Stored lifecycle status. "Scheduled", "Active", "Ended", and "Paused" are deliberately NOT
/// stored values — they are computed at read time from (Status, IsEnabled, StartsAtUtc, EndsAtUtc)
/// compared against the current instant, exactly like <see cref="Themes.ThemeSchedule.IsEffectiveAt"/>
/// already does for schedules. See <c>CampaignPhase</c> for the computed view.
/// </summary>
public enum CampaignStatus
{
    Draft = 0,
    Published = 1,
    Archived = 2,
}

/// <summary>
/// Computed, display-only phase — never persisted. Derived by <c>ThemeCampaign.GetPhase(DateTime)</c>.
/// </summary>
public enum CampaignPhase
{
    Draft = 0,
    Scheduled = 1,
    Active = 2,
    Ended = 3,
    Paused = 4,
    Archived = 5,
}

public enum CampaignAuditAction
{
    Created = 0,
    Updated = 1,
    ThemeChanged = 2,
    ScheduleChanged = 3,
    Published = 4,
    Enabled = 5,
    Disabled = 6,
    Archived = 7,
    Deleted = 8,
}
