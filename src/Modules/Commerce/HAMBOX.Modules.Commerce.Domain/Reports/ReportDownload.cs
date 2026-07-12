namespace HAMBOX.Modules.Commerce.Domain.Reports;

public sealed class ReportDownload
{
    private ReportDownload()
    {
    }

    private ReportDownload(
        Guid id,
        string userId,
        string reportType,
        string format,
        string? filtersJson,
        string fileName,
        long fileSizeBytes,
        string? correlationId)
    {
        Id = id;
        UserId = userId;
        ReportType = reportType;
        Format = format;
        FiltersJson = filtersJson;
        FileName = fileName;
        FileSizeBytes = fileSizeBytes;
        CorrelationId = correlationId;
        CreatedOnUtc = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }
    public string UserId { get; private set; } = string.Empty;
    public string ReportType { get; private set; } = string.Empty;
    public string Format { get; private set; } = string.Empty;
    public string? FiltersJson { get; private set; }
    public string FileName { get; private set; } = string.Empty;
    public long FileSizeBytes { get; private set; }
    public DateTimeOffset CreatedOnUtc { get; private set; }
    public string? CorrelationId { get; private set; }

    public static ReportDownload Create(
        string userId,
        string reportType,
        string format,
        string? filtersJson,
        string fileName,
        long fileSizeBytes,
        string? correlationId = null) =>
        new(
            Guid.NewGuid(),
            userId,
            reportType,
            format,
            filtersJson,
            fileName,
            fileSizeBytes,
            correlationId);
}
