using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Domain.Operations;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Commerce.Infrastructure.Services;

internal sealed class OperationalJobQueue(ICommerceDbContext db) : IOperationalJobQueue
{
    public async Task<OperationalJob> EnqueueAsync(
        string jobType,
        string? payloadJson = null,
        OperationalJobPriority priority = OperationalJobPriority.Normal,
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
            correlationId,
            relatedEntityType,
            relatedEntityId,
            maxAttempts ?? 3);

        db.OperationalJobs.Add(job);
        await db.SaveChangesAsync(cancellationToken);
        return job;
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
            .Where(j => j.Status == OperationalJobStatus.Failed)
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
