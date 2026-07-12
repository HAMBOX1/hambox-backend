using HAMBOX.Modules.Commerce.Application.Contracts.Reports;

namespace HAMBOX.Modules.Commerce.Application.Abstractions;

public interface IReportCatalog
{
    IReadOnlyList<ReportTypeInfoDto> GetTypes();
    ReportTypeInfoDto? GetType(string reportType);
}

public interface IReportBuilderService
{
    Task<ReportModel> BuildAsync(
        string reportType,
        ReportFilterRequest filters,
        CancellationToken cancellationToken = default);
}

public interface IReportDocumentGenerator
{
    Task<ReportDocumentResult> GenerateAsync(
        ReportDocumentRequest request,
        CancellationToken cancellationToken = default);
}

public interface IScheduledReportService
{
    Task<IReadOnlyList<ScheduledReportDto>> ListAsync(CancellationToken cancellationToken = default);

    Task<ScheduledReportDto> CreateAsync(
        string reportType,
        string? filtersJson,
        string format,
        string frequency,
        IReadOnlyList<string> emailRecipients,
        bool isEnabled,
        string? createdByUserId,
        Guid? reportDefinitionId = null,
        CancellationToken cancellationToken = default);

    Task<ScheduledReportDto?> UpdateAsync(
        Guid id,
        string? filtersJson,
        string format,
        string frequency,
        IReadOnlyList<string> emailRecipients,
        bool isEnabled,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Guid> EnqueueManualRunAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ScheduledReportDto>> GetDueSchedulesAsync(CancellationToken cancellationToken = default);

    Task ExecuteAsync(
        Guid scheduledReportId,
        string triggeredBy,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ScheduledReportExecutionDto>> GetExecutionsAsync(
        Guid scheduledReportId,
        CancellationToken cancellationToken = default);
}
