namespace HAMBOX.Modules.Themes.Application.Contracts.Themes;

public sealed record ThemeListItemDto(
    Guid Id,
    string Name,
    string Slug,
    string Status,
    string BaseMode,
    bool IsDefault,
    int VersionCount,
    DateTime? PublishedOnUtc,
    DateTimeOffset CreatedOnUtc,
    string? PreviewPrimary,
    string? PreviewBackground,
    string? PreviewCard,
    string? AssignmentSummary);

public sealed record ThemeTemplateDto(
    Guid Id,
    string Name,
    string? Description,
    string BaseMode,
    IReadOnlyDictionary<string, string> Tokens);

public sealed record ThemeDetailDto(
    Guid Id,
    string Name,
    string Slug,
    string? Description,
    string Status,
    string BaseMode,
    bool IsDefault,
    Guid? PublishedVersionId,
    int VersionCounter,
    IReadOnlyList<ThemeVersionDto> Versions,
    IReadOnlyList<ThemeAssetDto> Assets,
    IReadOnlyList<ThemeScheduleDto> Schedules,
    IReadOnlyList<ThemeAssignmentDto> Assignments);

public sealed record ThemeVersionDto(
    Guid Id,
    int VersionNumber,
    bool IsPublished,
    DateTime? PublishedOnUtc,
    string? Notes,
    IReadOnlyDictionary<string, string> Tokens,
    IReadOnlyList<string> ValidationWarnings);

public sealed record ThemeAssetDto(string AssetType, string Url, string? AltText);

public sealed record ThemeScheduleDto(
    Guid Id,
    DateTime StartsAtUtc,
    DateTime? EndsAtUtc,
    string? RecurrenceRule,
    bool IsActive);

public sealed record ThemeAssignmentDto(
    Guid Id,
    string AssignmentType,
    string TargetKey,
    int Priority,
    bool IsActive);

public sealed record CreateThemeRequest(
    string Name,
    string Slug,
    string? Description,
    string BaseMode,
    Dictionary<string, string>? Tokens);

public sealed record UpdateThemeRequest(
    string Name,
    string? Description,
    Dictionary<string, string> Tokens,
    string? Notes);

public sealed record DuplicateThemeRequest(string NewName, string NewSlug);

public sealed record ScheduleThemeRequest(
    DateTime StartsAtUtc,
    DateTime? EndsAtUtc,
    string? RecurrenceRule);

public sealed record AssignThemeRequest(
    string AssignmentType,
    string TargetKey,
    int Priority = 0);

public sealed record ThemeAssetRequest(string AssetType, string Url, string? AltText);

public sealed record ActiveThemeDto(
    Guid ThemeId,
    string ThemeName,
    string Slug,
    string BaseMode,
    Guid VersionId,
    int VersionNumber,
    IReadOnlyDictionary<string, string> Tokens,
    IReadOnlyList<ThemeAssetDto> Assets,
    string ResolutionSource);

public sealed record ThemePreviewSessionDto(string Token, DateTime ExpiresAtUtc, Guid ThemeId, Guid VersionId);

public sealed record ThemeStatisticsDto(
    int TotalThemes,
    int PublishedThemes,
    int DraftThemes,
    int ArchivedThemes,
    int ActiveSchedules,
    int ActiveAssignments);

public sealed record ThemeHistoryEntryDto(
    Guid Id,
    string Action,
    string? ActorUserId,
    DateTime CreatedOnUtc,
    string? DetailsJson);

public sealed record ThemeCompareDto(
    Guid LeftThemeId,
    Guid RightThemeId,
    IReadOnlyList<ThemeTokenDiffDto> TokenDiffs);

public sealed record ThemeTokenDiffDto(string Token, string? LeftValue, string? RightValue);

public sealed record ThemeValidationResultDto(
    bool IsValid,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings);

public sealed record ImportThemeRequest(
    string Name,
    string Slug,
    string BaseMode,
    Dictionary<string, string> Tokens,
    IReadOnlyList<ThemeAssetRequest>? Assets);

public sealed record ExportThemeDto(
    string Name,
    string Slug,
    string BaseMode,
    Dictionary<string, string> Tokens,
    IReadOnlyList<ThemeAssetDto> Assets,
    int VersionNumber);
