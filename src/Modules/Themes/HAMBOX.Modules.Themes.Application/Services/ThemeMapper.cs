using HAMBOX.Modules.Themes.Application.Contracts.Themes;
using HAMBOX.Modules.Themes.Domain.Themes;

namespace HAMBOX.Modules.Themes.Application.Services;

public static class ThemeMapper
{
    public static ThemeListItemDto ToListItem(
        StoreTheme theme,
        DateTime? publishedOnUtc,
        int versionCount,
        IReadOnlyDictionary<string, string>? previewTokens = null,
        string? assignmentSummary = null) =>
        new(
            theme.Id,
            theme.Name,
            theme.Slug,
            theme.Status.ToString(),
            theme.BaseMode.ToString(),
            theme.IsDefault,
            versionCount,
            publishedOnUtc,
            theme.CreatedOnUtc,
            previewTokens?.GetValueOrDefault(ThemeSemanticTokens.Primary),
            previewTokens?.GetValueOrDefault(ThemeSemanticTokens.Background),
            previewTokens?.GetValueOrDefault(ThemeSemanticTokens.Card),
            assignmentSummary);

    public static ThemeVersionDto ToVersionDto(ThemeVersion version)
    {
        IReadOnlyList<string> warnings = [];
        if (!string.IsNullOrWhiteSpace(version.ValidationWarningsJson))
        {
            warnings = System.Text.Json.JsonSerializer.Deserialize<List<string>>(version.ValidationWarningsJson) ?? [];
        }

        return new ThemeVersionDto(
            version.Id,
            version.VersionNumber,
            version.IsPublished,
            version.HasEverBeenPublished,
            version.PublishedOnUtc,
            version.Notes,
            version.GetTokens(),
            warnings);
    }

    public static ThemeDetailDto ToDetail(
        StoreTheme theme,
        IReadOnlyList<ThemeVersion> versions,
        IReadOnlyList<ThemeAsset> assets,
        IReadOnlyList<ThemeSchedule> schedules,
        IReadOnlyList<ThemeAssignment> assignments,
        IReadOnlySet<Guid>? overlappingScheduleIds = null) =>
        new(
            theme.Id,
            theme.Name,
            theme.Slug,
            theme.Description,
            theme.Status.ToString(),
            theme.BaseMode.ToString(),
            theme.IsDefault,
            theme.PublishedVersionId,
            theme.VersionCounter,
            versions.Select(ToVersionDto).ToList(),
            assets.Select(a => new ThemeAssetDto(a.AssetType.ToString(), a.Url, a.AltText)).ToList(),
            schedules.Select(s => new ThemeScheduleDto(
                s.Id,
                s.StartsAtUtc,
                s.EndsAtUtc,
                s.RecurrenceRule,
                s.Priority,
                s.IsActive,
                overlappingScheduleIds?.Contains(s.Id) ?? false)).ToList(),
            assignments.Select(a => new ThemeAssignmentDto(a.Id, a.AssignmentType.ToString(), a.TargetKey, a.Priority, a.IsActive)).ToList());

    public static Dictionary<string, string> DefaultTokens(ThemeBaseMode baseMode) =>
        baseMode == ThemeBaseMode.Dark ? DefaultDarkTokens() : DefaultLightTokens();

    public static Dictionary<string, string> DefaultDarkTokens() => new(StringComparer.OrdinalIgnoreCase)
    {
        [ThemeSemanticTokens.Primary] = "#50ed95",
        [ThemeSemanticTokens.Secondary] = "#b0c6ff",
        [ThemeSemanticTokens.Accent] = "#50ed95",
        [ThemeSemanticTokens.Success] = "#50ed95",
        [ThemeSemanticTokens.Warning] = "#fbbf24",
        [ThemeSemanticTokens.Danger] = "#f87171",
        [ThemeSemanticTokens.Background] = "#131314",
        [ThemeSemanticTokens.Surface] = "#1a1a1c",
        [ThemeSemanticTokens.SurfaceElevation] = "#2a2a2b",
        [ThemeSemanticTokens.Card] = "#2a2a2b",
        [ThemeSemanticTokens.Border] = "rgba(255, 255, 255, 0.1)",
        [ThemeSemanticTokens.Divider] = "rgba(255, 255, 255, 0.06)",
        [ThemeSemanticTokens.Typography] = "'Inter', system-ui, sans-serif",
        [ThemeSemanticTokens.BorderRadius] = "12px",
        [ThemeSemanticTokens.Spacing] = "16px",
        [ThemeSemanticTokens.Shadow] = "0 8px 24px rgba(0, 0, 0, 0.35)",
        [ThemeSemanticTokens.AnimationSpeed] = "200ms",
        [ThemeSemanticTokens.ButtonRadius] = "10px",
        [ThemeSemanticTokens.InputRadius] = "10px",
        [ThemeSemanticTokens.Navbar] = "rgba(19, 19, 20, 0.82)",
        [ThemeSemanticTokens.Sidebar] = "#0e0e0f",
        [ThemeSemanticTokens.Footer] = "#201f20",
    };

    public static Dictionary<string, string> DefaultLightTokens() => new(StringComparer.OrdinalIgnoreCase)
    {
        [ThemeSemanticTokens.Primary] = "#16a34a",
        [ThemeSemanticTokens.Secondary] = "#2563eb",
        [ThemeSemanticTokens.Accent] = "#16a34a",
        [ThemeSemanticTokens.Success] = "#16a34a",
        [ThemeSemanticTokens.Warning] = "#d97706",
        [ThemeSemanticTokens.Danger] = "#dc2626",
        [ThemeSemanticTokens.Background] = "#f8faf9",
        [ThemeSemanticTokens.Surface] = "#ffffff",
        [ThemeSemanticTokens.SurfaceElevation] = "#ffffff",
        [ThemeSemanticTokens.Card] = "#ffffff",
        [ThemeSemanticTokens.Border] = "rgba(15, 23, 42, 0.12)",
        [ThemeSemanticTokens.Divider] = "rgba(15, 23, 42, 0.08)",
        [ThemeSemanticTokens.Typography] = "'Inter', system-ui, sans-serif",
        [ThemeSemanticTokens.BorderRadius] = "12px",
        [ThemeSemanticTokens.Spacing] = "16px",
        [ThemeSemanticTokens.Shadow] = "0 8px 24px rgba(15, 23, 42, 0.08)",
        [ThemeSemanticTokens.AnimationSpeed] = "200ms",
        [ThemeSemanticTokens.ButtonRadius] = "10px",
        [ThemeSemanticTokens.InputRadius] = "10px",
        [ThemeSemanticTokens.Navbar] = "rgba(255, 255, 255, 0.92)",
        [ThemeSemanticTokens.Sidebar] = "#ffffff",
        [ThemeSemanticTokens.Footer] = "#f1f5f4",
    };
}
