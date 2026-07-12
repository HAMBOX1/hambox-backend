using HAMBOX.SharedKernel.Errors;

namespace HAMBOX.Modules.Themes.Application.Errors;

public static class ThemeErrors
{
    public static readonly Error ThemeNotFound = new("Theme.NotFound", "Theme not found.");
    public static readonly Error ThemeNoVersion = new("Theme.NoVersion", "Theme has no versions.");
    public static readonly Error ThemeNoActive = new("Theme.NoActive", "No active theme configured.");
    public static readonly Error ThemePreviewExpired = new("Theme.PreviewExpired", "Preview session expired or invalid.");
    public static readonly Error ThemeSlugConflict = new("Theme.SlugConflict", "Theme slug already exists.");
    public static readonly Error ThemeDefaultProtected = new("Theme.DefaultProtected", "Default theme cannot be deleted or archived.");
    public static readonly Error ThemeInvalidBaseMode = new("Theme.InvalidBaseMode", "Invalid base mode.");
    public static readonly Error ThemeInvalidAssignmentType = new("Theme.InvalidAssignmentType", "Invalid assignment type.");
    public static readonly Error ThemeInvalidAssetType = new("Theme.InvalidAssetType", "Invalid asset type.");
    public static readonly Error ThemeCompareFailed = new("Theme.CompareFailed", "Both themes must have published versions.");
    public static readonly Error ThemeValidationFailed = new("Theme.ValidationFailed", "Theme validation failed.");
}
