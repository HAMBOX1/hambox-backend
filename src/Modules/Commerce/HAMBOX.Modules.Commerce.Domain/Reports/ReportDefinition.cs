namespace HAMBOX.Modules.Commerce.Domain.Reports;

public sealed class ReportDefinition
{
    private ReportDefinition()
    {
    }

    private ReportDefinition(
        Guid id,
        string name,
        string reportType,
        string? filtersJson,
        string formatDefault,
        bool isSystem,
        string? createdByUserId)
    {
        Id = id;
        Name = name;
        ReportType = reportType;
        FiltersJson = filtersJson;
        FormatDefault = formatDefault;
        IsSystem = isSystem;
        CreatedByUserId = createdByUserId;
        CreatedOnUtc = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string ReportType { get; private set; } = string.Empty;
    public string? FiltersJson { get; private set; }
    public string FormatDefault { get; private set; } = ReportFormats.Pdf;
    public bool IsSystem { get; private set; }
    public string? CreatedByUserId { get; private set; }
    public DateTimeOffset CreatedOnUtc { get; private set; }
    public DateTimeOffset? ModifiedOnUtc { get; private set; }

    public static ReportDefinition Create(
        string name,
        string reportType,
        string? filtersJson,
        string formatDefault,
        string? createdByUserId,
        bool isSystem = false) =>
        new(
            Guid.NewGuid(),
            name.Trim(),
            reportType.Trim(),
            filtersJson,
            string.IsNullOrWhiteSpace(formatDefault) ? ReportFormats.Pdf : formatDefault.Trim().ToLowerInvariant(),
            isSystem,
            createdByUserId);

    public void Update(string name, string? filtersJson, string formatDefault)
    {
        Name = name.Trim();
        FiltersJson = filtersJson;
        FormatDefault = string.IsNullOrWhiteSpace(formatDefault)
            ? FormatDefault
            : formatDefault.Trim().ToLowerInvariant();
        ModifiedOnUtc = DateTimeOffset.UtcNow;
    }
}
