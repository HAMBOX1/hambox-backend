namespace HAMBOX.Modules.Commerce.Application.Contracts.Reports;

public sealed record ReportTypeInfoDto(
    string Type,
    string Name,
    string Description,
    string Category,
    IReadOnlyList<string> SupportedFormats);

public sealed record ReportFilterRequest(
    string? Preset,
    DateTimeOffset? From,
    DateTimeOffset? To,
    string? Status,
    Guid? CategoryId,
    Guid? MembershipPlanId,
    Guid? PromotionId,
    string? Country,
    string? Currency);

public sealed record ReportKpiDto(string Label, string Value, string? Unit = null);

public sealed record ReportTableColumnDto(string Key, string Header);

public sealed record ReportTableRowDto(IReadOnlyDictionary<string, string> Cells);

public sealed record ReportTableDto(
    string Title,
    IReadOnlyList<ReportTableColumnDto> Columns,
    IReadOnlyList<ReportTableRowDto> Rows);

public sealed record ReportChartSeriesDto(
    string Title,
    IReadOnlyList<string> Labels,
    IReadOnlyList<decimal> Values);

public sealed record ReportSectionDto(
    string Title,
    IReadOnlyList<ReportKpiDto>? Kpis = null,
    ReportTableDto? Table = null,
    ReportChartSeriesDto? Chart = null);

public sealed record ReportModel(
    string ReportType,
    string Title,
    string? BrandName,
    DateTimeOffset GeneratedOnUtc,
    IReadOnlyDictionary<string, string> FiltersSummary,
    IReadOnlyList<ReportSectionDto> Sections,
    IReadOnlyList<ReportKpiDto>? Totals = null);

public sealed record ReportDocumentRequest(
    ReportModel Model,
    string Format);

public sealed record ReportDocumentResult(
    byte[] Content,
    string ContentType,
    string FileName);

public sealed record ReportDefinitionDto(
    Guid Id,
    string Name,
    string ReportType,
    string? FiltersJson,
    string FormatDefault,
    bool IsSystem,
    string? CreatedByUserId,
    DateTimeOffset CreatedOnUtc,
    DateTimeOffset? ModifiedOnUtc,
    bool IsFavorite);

public sealed record ReportDownloadDto(
    Guid Id,
    string UserId,
    string ReportType,
    string Format,
    string FileName,
    long FileSizeBytes,
    DateTimeOffset CreatedOnUtc,
    string? CorrelationId);

public sealed record ScheduledReportDto(
    Guid Id,
    Guid? ReportDefinitionId,
    string ReportType,
    string? FiltersJson,
    string Format,
    string Frequency,
    IReadOnlyList<string> EmailRecipients,
    bool IsEnabled,
    DateTimeOffset? NextRunOnUtc,
    DateTimeOffset? LastRunOnUtc,
    string? CreatedByUserId,
    DateTimeOffset CreatedOnUtc);

public sealed record ScheduledReportExecutionDto(
    Guid Id,
    Guid ScheduledReportId,
    string Status,
    DateTimeOffset StartedOnUtc,
    DateTimeOffset? FinishedOnUtc,
    string? Error,
    Guid? DownloadId,
    string TriggeredBy);
