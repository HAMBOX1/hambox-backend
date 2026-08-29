using HAMBOX.Application.BackgroundJobs;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Domain.Operations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HAMBOX.Modules.Commerce.Infrastructure.Services;

/// <summary>
/// The default background-job engine's storage: implements the Commerce-internal
/// <see cref="IOperationalJobQueue"/> and the cross-module <see cref="IBackgroundJobScheduler"/> on
/// the same table — one enqueue path, two surfaces. A future engine (Hangfire, etc.) replaces this
/// class's <see cref="IBackgroundJobScheduler"/> registration only; no caller of either interface changes.
/// </summary>
internal sealed class OperationalJobQueue(
    ICommerceDbContext db, IBackgroundJobSerializer serializer, IJobQueueNotifier notifier, ILogger<OperationalJobQueue> logger)
    : IOperationalJobQueue, IBackgroundJobScheduler
{
    public async Task<OperationalJob> EnqueueAsync(
        string jobType,
        string? payloadJson = null,
        OperationalJobPriority priority = OperationalJobPriority.Normal,
        string queue = OperationalJobQueues.Default,
        string? correlationId = null,
        string? relatedEntityType = null,
        string? relatedEntityId = null,
        int? maxAttempts = null,
        CancellationToken cancellationToken = default)
    {
        var job = OperationalJob.Create(
            jobType,
            payloadJson,
            priority,
            queue,
            correlationId,
            relatedEntityType,
            relatedEntityId,
            maxAttempts ?? 3);

        db.OperationalJobs.Add(job);
        await db.SaveChangesAsync(cancellationToken);

        // The SQL row above is the durable job — already committed by the time this runs. This is
        // purely "consider waking up sooner than the next scheduled poll"; see IJobQueueNotifier.
        // Defensive catch here too, on top of every implementation's own "never throws" contract —
        // enqueueing a job (and the caller that triggered it, e.g. checkout) must never fail just
        // because a wake-up notification couldn't be published.
        try
        {
            await notifier.NotifyAsync(queue, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Job wake-up notification failed for job {JobId} on queue '{Queue}' — the job itself was still enqueued successfully.", job.Id, queue);
        }

        return job;
    }

    public async Task<Guid> EnqueueAsync<TPayload>(
        string jobType,
        TPayload payload,
        BackgroundJobOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var job = await EnqueueCoreAsync(jobType, serializer.Serialize(payload), options, cancellationToken);
        return job.Id;
    }

    public async Task<Guid> EnqueueAsync(
        string jobType,
        BackgroundJobOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var job = await EnqueueCoreAsync(jobType, null, options, cancellationToken);
        return job.Id;
    }

    private Task<OperationalJob> EnqueueCoreAsync(
        string jobType,
        string? payloadJson,
        BackgroundJobOptions? options,
        CancellationToken cancellationToken)
    {
        options ??= new BackgroundJobOptions();
        return EnqueueAsync(
            jobType,
            payloadJson,
            (OperationalJobPriority)options.Priority,
            options.Queue,
            options.CorrelationId,
            options.RelatedEntityType,
            options.RelatedEntityId,
            options.MaxAttempts,
            cancellationToken);
    }

    public async Task<IReadOnlyList<OperationalJob>> ClaimNextBatchAsync(
        string workerId,
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var size = Math.Clamp(batchSize, 1, 100);

        var candidates = await db.OperationalJobs
            .Where(j =>
                j.Status == OperationalJobStatus.Queued
                || (j.Status == OperationalJobStatus.Retrying
                    && (j.NextVisibleOnUtc == null || j.NextVisibleOnUtc <= now)))
            .OrderByDescending(j => j.Priority)
            .ThenBy(j => j.CreatedOnUtc)
            .Take(size)
            .ToListAsync(cancellationToken);

        foreach (var job in candidates)
        {
            job.MarkRunning(workerId);
        }

        if (candidates.Count > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        return candidates;
    }

    public async Task<bool> RetryAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        var job = await db.OperationalJobs.FirstOrDefaultAsync(j => j.Id == jobId, cancellationToken);
        if (job is null)
        {
            return false;
        }

        job.RequeueForRetry();
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<int> RetryAllFailedAsync(CancellationToken cancellationToken = default)
    {
        var failed = await db.OperationalJobs
            .Where(j => j.Status == OperationalJobStatus.Failed || j.Status == OperationalJobStatus.DeadLetter)
            .ToListAsync(cancellationToken);

        foreach (var job in failed)
        {
            job.RequeueForRetry();
        }

        if (failed.Count > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        return failed.Count;
    }

    public async Task<bool> CancelAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        var job = await db.OperationalJobs.FirstOrDefaultAsync(j => j.Id == jobId, cancellationToken);
        if (job is null)
        {
            return false;
        }

        job.Cancel();
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<int> ClearCompletedAsync(CancellationToken cancellationToken = default)
    {
        var completed = await db.OperationalJobs
            .Where(j => j.Status == OperationalJobStatus.Completed)
            .ToListAsync(cancellationToken);

        db.OperationalJobs.RemoveRange(completed);
        if (completed.Count > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        return completed.Count;
    }
}
