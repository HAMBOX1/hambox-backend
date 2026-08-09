namespace HAMBOX.Modules.Themes.Domain.Themes;

public enum StoreThemeStatus
{
    Draft = 0,
    Published = 1,
    Archived = 2,
}

public enum ThemeBaseMode
{
    Dark = 0,
    Light = 1,
}

public enum ThemeAssignmentType
{
    Store = 0,
    Membership = 1,

    /// <summary>
    /// Reserved — never resolved by ThemeEngine and rejected by AssignThemeCommandHandler for new
    /// assignments. The real, working mechanism for time-boxed marketing overrides is the
    /// dedicated <see cref="Campaigns.ThemeCampaign"/> aggregate, not this assignment type. The
    /// numeric value is preserved (not renumbered/reused) in case any pre-existing row references
    /// it — confirmed via repo-wide audit that no seed data or code path ever constructs this
    /// value, but a row created through a direct API call prior to this change can't be ruled out
    /// without inspecting the live database.
    /// </summary>
    [Obsolete("Non-functional — never resolved by ThemeEngine. Use the ThemeCampaign aggregate instead.")]
    Campaign = 2,

    /// <summary>Reserved — no HAMBOX use case exists for region-based theming. See Campaign's remarks.</summary>
    [Obsolete("Non-functional — never resolved by ThemeEngine. No region-based theming use case exists.")]
    Region = 3,

    /// <summary>Reserved — HAMBOX is single-tenant; no use case exists. See Campaign's remarks.</summary>
    [Obsolete("Non-functional — never resolved by ThemeEngine. HAMBOX is single-tenant.")]
    Tenant = 4,
}

public enum ThemeAssetType
{
    Logo = 0,
    DarkLogo = 1,
    LightLogo = 2,
    Favicon = 3,
    HeroBackground = 4,
    StoreBanner = 5,
    FooterImage = 6,
}

public enum ThemeAuditAction
{
    Created = 0,
    Edited = 1,
    Published = 2,
    Scheduled = 3,
    Assigned = 4,
    Deleted = 5,
    Restored = 6,
    Imported = 7,
    Exported = 8,
    RolledBack = 9,
    Archived = 10,
    Duplicated = 11,
}
