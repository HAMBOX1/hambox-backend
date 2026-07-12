namespace HAMBOX.Modules.Commerce.Domain.Reports;

public sealed class ScheduledReportExecution
{
    private ScheduledReportExecution()
    {
    }

    private ScheduledReportExecution(
        Guid id,
        Guid scheduledReportId,
        string status,
        string triggeredBy)
    {
        Id = id;
        ScheduledReportId = scheduledReportId;
        Status = status;
        TriggeredBy = triggeredBy;
        StartedOnUtc = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid ScheduledReportId { get; private set; }
    public string Status { get; private set; } = ScheduledReportExecutionStatuses.Pending;
    public DateTimeOffset StartedOnUtc { get; private set; }
    public DateTimeOffset? FinishedOnUtc { get; private set; }
    public string? Error { get; private set; }
    public Guid? DownloadId { get; private set; }
    public string TriggeredBy { get; private set; } = ScheduledReportTriggers.Schedule;

    public static ScheduledReportExecution Create(Guid scheduledReportId, string triggeredBy) =>
        new(
            Guid.NewGuid(),
            scheduledReportId,
            ScheduledReportExecutionStatuses.Pending,
            string.IsNullOrWhiteSpace(triggeredBy) ? ScheduledReportTriggers.Schedule : triggeredBy);

    public void MarkRunning()
    {
        Status = ScheduledReportExecutionStatuses.Running;
        StartedOnUtc = DateTimeOffset.UtcNow;
    }

    public void MarkSucceeded(Guid? downloadId)
    {
        Status = ScheduledReportExecutionStatuses.Succeeded;
        DownloadId = downloadId;
        FinishedOnUtc = DateTimeOffset.UtcNow;
        Error = null;
    }

    public void MarkFailed(string error)
    {
        Status = ScheduledReportExecutionStatuses.Failed;
        Error = error.Length > 2000 ? error[..2000] : error;
        FinishedOnUtc = DateTimeOffset.UtcNow;
    }

    public void MarkSkipped(string? reason = null)
    {
        Status = ScheduledReportExecutionStatuses.Skipped;
        Error = reason;
        FinishedOnUtc = DateTimeOffset.UtcNow;
    }
}
