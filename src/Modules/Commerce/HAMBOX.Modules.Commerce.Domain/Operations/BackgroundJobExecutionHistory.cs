namespace HAMBOX.Modules.Commerce.Domain.Operations;

/// <summary>
/// One row per job attempt — the "Job History" the framework persists distinct from
/// <see cref="OperationalJob"/>'s own live-status row, so retries don't overwrite what happened
/// on earlier attempts.
/// </summary>
public sealed class BackgroundJobExecutionHistory
{
    private BackgroundJobExecutionHistory()
    {
    }

    private BackgroundJobExecutionHistory(
        Guid id,
        Guid jobId,
        int attemptNumber,
        DateTimeOffset startedOnUtc,
        string? workerId,
        string? correlationId)
    {
        Id = id;
        JobId = jobId;
        AttemptNumber = attemptNumber;
        StartedOnUtc = startedOnUtc;
        WorkerId = workerId;
        CorrelationId = correlationId;
        Status = OperationalJobStatus.Running;
    }

    public Guid Id { get; private set; }
    public Guid JobId { get; private set; }
    public int AttemptNumber { get; private set; }
    public OperationalJobStatus Status { get; private set; }
    public DateTimeOffset StartedOnUtc { get; private set; }
    public DateTimeOffset? FinishedOnUtc { get; private set; }
    public long? DurationMs { get; private set; }
    public string? Exception { get; private set; }
    public string? WorkerId { get; private set; }
    public string? CorrelationId { get; private set; }

    public static BackgroundJobExecutionHistory Start(
        Guid jobId, int attemptNumber, string? workerId, string? correlationId) =>
        new(Guid.NewGuid(), jobId, attemptNumber, DateTimeOffset.UtcNow, workerId, correlationId);

    public void Complete(OperationalJobStatus status, string? exception)
    {
        Status = status;
        Exception = exception;
        FinishedOnUtc = DateTimeOffset.UtcNow;
        DurationMs = (long)(FinishedOnUtc.Value - StartedOnUtc).TotalMilliseconds;
    }
}
