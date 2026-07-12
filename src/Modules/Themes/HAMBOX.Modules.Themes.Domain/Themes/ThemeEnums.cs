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
    Campaign = 2,
    Region = 3,
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
