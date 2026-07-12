using System.Text.RegularExpressions;
using HAMBOX.Modules.Themes.Application.Contracts.Themes;
using HAMBOX.Modules.Themes.Domain.Themes;

namespace HAMBOX.Modules.Themes.Application.Services;

public static class ThemeValidator
{
    private static readonly Regex HexColor = new(@"^#([0-9a-fA-F]{3}|[0-9a-fA-F]{6}|[0-9a-fA-F]{8})$", RegexOptions.Compiled);

    public static ThemeValidationResultDto Validate(Dictionary<string, string> tokens, IReadOnlyList<ThemeAssetDto>? assets = null)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        foreach (var required in ThemeSemanticTokens.Required)
        {
            if (!tokens.TryGetValue(required, out var value) || string.IsNullOrWhiteSpace(value))
            {
                errors.Add($"Missing required token: {required}");
            }
        }

        foreach (var (key, value) in tokens)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            if (IsColorToken(key) && !HexColor.IsMatch(value) && !value.StartsWith("rgb", StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"Token '{key}' must be a valid color value.");
            }
        }

        if (tokens.TryGetValue(ThemeSemanticTokens.Primary, out var primary) &&
            tokens.TryGetValue(ThemeSemanticTokens.Background, out var background) &&
            IsLowContrast(primary, background))
        {
            warnings.Add("Primary color may have insufficient contrast against background.");
        }

        var requiredAssets = new[] { ThemeAssetType.Favicon.ToString() };
        if (assets is not null)
        {
            foreach (var assetType in requiredAssets)
            {
                if (!assets.Any(a => a.AssetType.Equals(assetType, StringComparison.OrdinalIgnoreCase)))
                {
                    warnings.Add($"Missing recommended asset: {assetType}");
                }
            }
        }

        return new ThemeValidationResultDto(errors.Count == 0, errors, warnings);
    }

    private static bool IsColorToken(string key) =>
        key is ThemeSemanticTokens.Primary or ThemeSemanticTokens.Secondary or ThemeSemanticTokens.Accent
            or ThemeSemanticTokens.Success or ThemeSemanticTokens.Warning or ThemeSemanticTokens.Danger
            or ThemeSemanticTokens.Background or ThemeSemanticTokens.Surface or ThemeSemanticTokens.SurfaceElevation
            or ThemeSemanticTokens.Card or ThemeSemanticTokens.Border or ThemeSemanticTokens.Divider
            or ThemeSemanticTokens.Navbar or ThemeSemanticTokens.Sidebar or ThemeSemanticTokens.Footer;

    private static bool IsLowContrast(string foreground, string background)
    {
        if (!HexColor.IsMatch(foreground) || !HexColor.IsMatch(background))
        {
            return false;
        }

        var fg = ParseHex(foreground);
        var bg = ParseHex(background);
        var ratio = ContrastRatio(fg, bg);
        return ratio < 3.0;
    }

    private static (double R, double G, double B) ParseHex(string hex)
    {
        hex = hex.TrimStart('#');
        if (hex.Length == 3)
        {
            hex = string.Concat(hex.Select(c => $"{c}{c}"));
        }

        var r = Convert.ToInt32(hex[..2], 16) / 255.0;
        var g = Convert.ToInt32(hex[2..4], 16) / 255.0;
        var b = Convert.ToInt32(hex[4..6], 16) / 255.0;
        return (r, g, b);
    }

    private static double ContrastRatio((double R, double G, double B) a, (double R, double G, double B) b)
    {
        var l1 = RelativeLuminance(a);
        var l2 = RelativeLuminance(b);
        var lighter = Math.Max(l1, l2);
        var darker = Math.Min(l1, l2);
        return (lighter + 0.05) / (darker + 0.05);
    }

    private static double RelativeLuminance((double R, double G, double B) c)
    {
        static double Transform(double channel) =>
            channel <= 0.03928 ? channel / 12.92 : Math.Pow((channel + 0.055) / 1.055, 2.4);

        return 0.2126 * Transform(c.R) + 0.7152 * Transform(c.G) + 0.0722 * Transform(c.B);
    }
}
