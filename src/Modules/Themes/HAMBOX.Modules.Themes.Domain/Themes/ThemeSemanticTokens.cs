namespace HAMBOX.Modules.Themes.Domain.Themes;

/// <summary>
/// Semantic design tokens exposed to admins. Values map to CSS custom properties at runtime.
/// </summary>
public static class ThemeSemanticTokens
{
    public const string Primary = "primary";
    public const string Secondary = "secondary";
    public const string Accent = "accent";
    public const string Success = "success";
    public const string Warning = "warning";
    public const string Danger = "danger";
    public const string Background = "background";
    public const string Surface = "surface";
    public const string SurfaceElevation = "surfaceElevation";
    public const string Card = "card";
    public const string Border = "border";
    public const string Divider = "divider";
    public const string Typography = "typography";
    public const string BorderRadius = "borderRadius";
    public const string Spacing = "spacing";
    public const string Shadow = "shadow";
    public const string AnimationSpeed = "animationSpeed";
    public const string ButtonRadius = "buttonRadius";
    public const string InputRadius = "inputRadius";
    public const string Navbar = "navbar";
    public const string Sidebar = "sidebar";
    public const string Footer = "footer";

    public static readonly IReadOnlyList<string> Required =
    [
        Primary, Background, Surface, Card, Border, Typography,
    ];

    public static readonly IReadOnlyDictionary<string, string> CssVariableMap =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [Primary] = "--theme-primary",
            [Secondary] = "--theme-info",
            [Accent] = "--theme-cta-gradient-start",
            [Success] = "--theme-success",
            [Warning] = "--theme-warning",
            [Danger] = "--theme-danger",
            [Background] = "--theme-background",
            [Surface] = "--theme-surface",
            [SurfaceElevation] = "--theme-elevated",
            [Card] = "--theme-card",
            [Border] = "--theme-border",
            [Divider] = "--theme-divider",
            [Typography] = "--hambox-font-family",
            [BorderRadius] = "--hambox-radius-md",
            [Spacing] = "--hambox-spacing-md",
            [Shadow] = "--theme-shadow-md",
            [AnimationSpeed] = "--hambox-transition-default",
            [ButtonRadius] = "--hambox-radius-sm",
            [InputRadius] = "--hambox-radius-sm",
            [Navbar] = "--theme-nav-bg",
            [Sidebar] = "--theme-sidebar",
            [Footer] = "--theme-footer-card",
        };
}
