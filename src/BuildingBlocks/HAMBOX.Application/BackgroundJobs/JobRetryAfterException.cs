namespace HAMBOX.Application.BackgroundJobs;

/// <summary>
/// Thrown by a job handler when the thing it depends on said "try again after N seconds" (a supplier's 429 with
/// Retry-After). The worker reschedules the job no sooner than <see cref="RetryAfter"/> instead of using its
/// generic retry delay (audit R41).
/// </summary>
public sealed class JobRetryAfterException(TimeSpan retryAfter, string message)
    : Exception(message)
{
    public TimeSpan RetryAfter { get; } = retryAfter;
}
