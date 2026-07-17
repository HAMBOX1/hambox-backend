namespace HAMBOX.Modules.Commerce.Domain.Operations;

public enum OperationalJobStatus
{
    Queued = 0,
    Running = 1,
    Completed = 2,
    Failed = 3,
    Retrying = 4,
    Cancelled = 5,
    DeadLetter = 6,
}

public enum OperationalJobPriority
{
    Low = 0,
    Normal = 1,
    High = 2,
    Critical = 3,
}

public static class OperationalJobTypes
{
    public const string ExpireInventoryReservations = "ExpireInventoryReservations";
    public const string RetryOrderFulfillment = "RetryOrderFulfillment";
    public const string RetryDelivery = "RetryDelivery";
    public const string HealthProbe = "HealthProbe";
    public const string SupplierHealthCheck = "SupplierHealthCheck";
    public const string GenerateOperationalAlerts = "GenerateOperationalAlerts";
}

/// <summary>
/// Mirrors <c>HAMBOX.Application.BackgroundJobs.BackgroundJobQueues</c> as string literals — the
/// Domain layer cannot reference Application, so queue names are duplicated as plain constants here.
/// </summary>
public static class OperationalJobQueues
{
    public const string Default = "default";
}

public sealed class OperationalJob
{
    private OperationalJob()
    {
    }

    private OperationalJob(
        Guid id,
        string jobType,
        string? payloadJson,
        OperationalJobPriority priority,
        string queue,
        string? correlationId,
        string? relatedEntityType,
        string? relatedEntityId,
        int maxAttempts)
    {
        Id = id;
        JobType = jobType;
        PayloadJson = payloadJson;
        Priority = priority;
        Queue = queue;
        CorrelationId = correlationId;
        RelatedEntityType = relatedEntityType;
        RelatedEntityId = relatedEntityId;
        MaxAttempts = maxAttempts;
        Status = OperationalJobStatus.Queued;
        CreatedOnUtc = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }
    public string JobType { get; private set; } = string.Empty;
    public string? PayloadJson { get; private set; }
    public OperationalJobStatus Status { get; private set; }
    public OperationalJobPriority Priority { get; private set; }
    public string Queue { get; private set; } = OperationalJobQueues.Default;
    public int ProgressPercent { get; private set; }
    public int Attempts { get; private set; }
    public int MaxAttempts { get; private set; }
    public string? WorkerId { get; private set; }
    public string? CorrelationId { get; private set; }
    public string? RelatedEntityType { get; private set; }
    public string? RelatedEntityId { get; private set; }
    public string? LastError { get; private set; }
    public string? ExceptionDetails { get; private set; }
    public DateTimeOffset CreatedOnUtc { get; private set; }
    public DateTimeOffset? StartedOnUtc { get; private set; }
    public DateTimeOffset? FinishedOnUtc { get; private set; }
    public DateTimeOffset? LastRetryOnUtc { get; private set; }
    public DateTimeOffset? NextVisibleOnUtc { get; private set; }

    public static OperationalJob Create(
        string jobType,
        string? payloadJson = null,
        OperationalJobPriority priority = OperationalJobPriority.Normal,
        string queue = OperationalJobQueues.Default,
        string? correlationId = null,
        string? relatedEntityType = null,
        string? relatedEntityId = null,
        int maxAttempts = 3)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jobType);
        return new OperationalJob(
            Guid.NewGuid(),
            jobType.Trim(),
            payloadJson,
            priority,
            string.IsNullOrWhiteSpace(queue) ? OperationalJobQueues.Default : queue.Trim(),
            correlationId,
            relatedEntityType,
            relatedEntityId,
            Math.Max(1, maxAttempts));
    }

    public void MarkRunning(string workerId)
    {
        Status = OperationalJobStatus.Running;
        WorkerId = workerId;
        StartedOnUtc ??= DateTimeOffset.UtcNow;
        Attempts += 1;
        NextVisibleOnUtc = null;
    }

    public void MarkCompleted()
    {
        Status = OperationalJobStatus.Completed;
        FinishedOnUtc = DateTimeOffset.UtcNow;
        LastError = null;
        ExceptionDetails = null;
    }

    public void MarkFailed(string error, string? exceptionDetails, TimeSpan? retryDelay, int? deadLetterThreshold = null)
    {
        LastError = error;
        ExceptionDetails = exceptionDetails;
        FinishedOnUtc = DateTimeOffset.UtcNow;

        if (deadLetterThreshold.HasValue && Attempts >= deadLetterThreshold.Value)
        {
            Status = OperationalJobStatus.DeadLetter;
            NextVisibleOnUtc = null;
            return;
        }

        if (Attempts < MaxAttempts && retryDelay.HasValue)
        {
            Status = OperationalJobStatus.Retrying;
            LastRetryOnUtc = DateTimeOffset.UtcNow;
            NextVisibleOnUtc = DateTimeOffset.UtcNow.Add(retryDelay.Value);
            return;
        }

        Status = OperationalJobStatus.Failed;
    }

    public void SetProgress(int percent) => ProgressPercent = Math.Clamp(percent, 0, 100);

    public void RequeueForRetry()
    {
        if (Status is not (OperationalJobStatus.Failed or OperationalJobStatus.Cancelled
            or OperationalJobStatus.Retrying or OperationalJobStatus.DeadLetter))
        {
            throw new InvalidOperationException("Only failed, cancelled, retrying, or dead-letter jobs can be requeued.");
        }

        Status = OperationalJobStatus.Queued;
        WorkerId = null;
        StartedOnUtc = null;
        FinishedOnUtc = null;
        NextVisibleOnUtc = null;
        LastError = null;
        ExceptionDetails = null;
        ProgressPercent = 0;
    }

    public void Cancel()
    {
        if (Status is OperationalJobStatus.Completed)
        {
            throw new InvalidOperationException("Completed jobs cannot be cancelled.");
        }

        Status = OperationalJobStatus.Cancelled;
        FinishedOnUtc = DateTimeOffset.UtcNow;
        NextVisibleOnUtc = null;
    }

    public bool IsClaimable(DateTimeOffset utcNow) =>
        Status is OperationalJobStatus.Queued
        || (Status is OperationalJobStatus.Retrying && (NextVisibleOnUtc is null || NextVisibleOnUtc <= utcNow));
}
