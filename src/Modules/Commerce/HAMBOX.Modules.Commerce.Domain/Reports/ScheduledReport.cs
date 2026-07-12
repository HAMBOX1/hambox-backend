namespace HAMBOX.Modules.Commerce.Domain.Reports;

public sealed class ScheduledReport
{
    private ScheduledReport()
    {
    }

    private ScheduledReport(
        Guid id,
        Guid? reportDefinitionId,
        string reportType,
        string? filtersJson,
        string format,
        string frequency,
        string emailRecipientsJson,
        bool isEnabled,
        DateTimeOffset? nextRunOnUtc,
        string? createdByUserId)
    {
        Id = id;
        ReportDefinitionId = reportDefinitionId;
        ReportType = reportType;
        FiltersJson = filtersJson;
        Format = format;
        Frequency = frequency;
        EmailRecipientsJson = emailRecipientsJson;
        IsEnabled = isEnabled;
        NextRunOnUtc = nextRunOnUtc;
        CreatedByUserId = createdByUserId;
        CreatedOnUtc = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid? ReportDefinitionId { get; private set; }
    public string ReportType { get; private set; } = string.Empty;
    public string? FiltersJson { get; private set; }
    public string Format { get; private set; } = ReportFormats.Pdf;
    public string Frequency { get; private set; } = ReportScheduleFrequencies.Daily;
    public string EmailRecipientsJson { get; private set; } = "[]";
    public bool IsEnabled { get; private set; }
    public DateTimeOffset? NextRunOnUtc { get; private set; }
    public DateTimeOffset? LastRunOnUtc { get; private set; }
    public string? CreatedByUserId { get; private set; }
    public DateTimeOffset CreatedOnUtc { get; private set; }
    public DateTimeOffset? ModifiedOnUtc { get; private set; }

    public static ScheduledReport Create(
        string reportType,
        string? filtersJson,
        string format,
        string frequency,
        string emailRecipientsJson,
        bool isEnabled,
        string? createdByUserId,
        Guid? reportDefinitionId = null)
    {
        var next = ComputeNextRun(frequency, DateTimeOffset.UtcNow);
        return new(
            Guid.NewGuid(),
            reportDefinitionId,
            reportType.Trim(),
            filtersJson,
            string.IsNullOrWhiteSpace(format) ? ReportFormats.Pdf : format.Trim().ToLowerInvariant(),
            NormalizeFrequency(frequency),
            string.IsNullOrWhiteSpace(emailRecipientsJson) ? "[]" : emailRecipientsJson,
            isEnabled,
            next,
            createdByUserId);
    }

    public void Update(
        string? filtersJson,
        string format,
        string frequency,
        string emailRecipientsJson,
        bool isEnabled)
    {
        FiltersJson = filtersJson;
        Format = string.IsNullOrWhiteSpace(format) ? Format : format.Trim().ToLowerInvariant();
        Frequency = NormalizeFrequency(frequency);
        EmailRecipientsJson = string.IsNullOrWhiteSpace(emailRecipientsJson) ? "[]" : emailRecipientsJson;
        IsEnabled = isEnabled;
        NextRunOnUtc = ComputeNextRun(Frequency, DateTimeOffset.UtcNow);
        ModifiedOnUtc = DateTimeOffset.UtcNow;
    }

    public void MarkRun(DateTimeOffset finishedOnUtc)
    {
        LastRunOnUtc = finishedOnUtc;
        NextRunOnUtc = ComputeNextRun(Frequency, finishedOnUtc);
        ModifiedOnUtc = finishedOnUtc;
    }

    public static DateTimeOffset ComputeNextRun(string frequency, DateTimeOffset fromUtc)
    {
        var normalized = NormalizeFrequency(frequency);
        return normalized switch
        {
            ReportScheduleFrequencies.Weekly => fromUtc.AddDays(7),
            ReportScheduleFrequencies.Monthly => fromUtc.AddMonths(1),
            _ => fromUtc.AddDays(1),
        };
    }

    private static string NormalizeFrequency(string frequency)
    {
        if (string.Equals(frequency, ReportScheduleFrequencies.Weekly, StringComparison.OrdinalIgnoreCase))
        {
            return ReportScheduleFrequencies.Weekly;
        }

        if (string.Equals(frequency, ReportScheduleFrequencies.Monthly, StringComparison.OrdinalIgnoreCase))
        {
            return ReportScheduleFrequencies.Monthly;
        }

        return ReportScheduleFrequencies.Daily;
    }
}
