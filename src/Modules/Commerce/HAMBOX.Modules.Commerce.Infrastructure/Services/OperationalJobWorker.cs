using HAMBOX.Application.Abstractions;
using HAMBOX.Application.BackgroundJobs;
using HAMBOX.Application.PlatformSettings;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Domain.Operations;
using HAMBOX.Modules.Commerce.Infrastructure.BackgroundJobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HAMBOX.Modules.Commerce.Infrastructure.Services;

/// <summary>
/// The default background-job engine: a polling <see cref="BackgroundService"/> that claims queued
/// <see cref="OperationalJob"/> rows and dispatches each to the <see cref="IBackgroundJobHandler"/>
/// registered for its job type. This class — plus <see cref="OperationalJobQueue"/> and the recurring-job
/// registry — is the only place that knows jobs are stored in SQL and polled; every handler and every
/// caller of <see cref="IBackgroundJobScheduler"/>/<see cref="IRecurringJobScheduler"/> is unaware of it.
/// </summary>
internal sealed class OperationalJobWorker(
    IServiceScopeFactory scopeFactory,
    IWorkerRuntimeState workerState,
    IRecurringJobScheduler recurringJobScheduler,
    ILogger<OperationalJobWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        workerState.MarkStarted();
        RegisterBuiltInRecurringJobs();
        logger.LogInformation("Operational job worker {WorkerId} started", workerState.WorkerId);

        while (!stoppingToken.IsCancellationRequested)
        {
            var delaySeconds = 30;
            try
            {
                workerState.Heartbeat();
                workerState.MarkTick();

                using var scope = scopeFactory.CreateScope();
                var platformSettings = scope.ServiceProvider.GetRequiredService<IPlatformSettingsProvider>();
                var queue = scope.ServiceProvider.GetRequiredService<IOperationalJobQueue>();
                var scheduler = scope.ServiceProvider.GetRequiredService<IBackgroundJobScheduler>();
                var recurringJobs = scope.ServiceProvider.GetRequiredService<IRecurringJobRegistry>();
                var commerceDb = scope.ServiceProvider.GetRequiredService<ICommerceDbContext>();

                var queueSettings = await platformSettings.GetAsync<QueueSettingsPayload>(
                    PlatformSettingsCategoryKeys.Queue,
                    stoppingToken);
                delaySeconds = Math.Clamp(queueSettings.WorkerIntervalSeconds, 5, 300);

                var retrySettings = await platformSettings.GetAsync<RetryPoliciesSettingsPayload>(
                    PlatformSettingsCategoryKeys.RetryPolicies,
                    stoppingToken);

                await EnsureRecurringJobsAsync(scheduler, recurringJobs, commerceDb, stoppingToken);

                var batch = await queue.ClaimNextBatchAsync(
                    workerState.WorkerId,
                    Math.Clamp(queueSettings.BatchSize, 1, 50),
                    stoppingToken);

                foreach (var job in batch)
                {
                    await ProcessJobAsync(scope.ServiceProvider, job, retrySettings, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Operational job worker tick failed");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(delaySeconds), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        workerState.MarkStopped();
        logger.LogInformation("Operational job worker {WorkerId} stopped", workerState.WorkerId);
    }

    /// <summary>
    /// Declares Commerce's own periodic jobs through <see cref="IRecurringJobScheduler"/> — the same
    /// call any other module would make from its own Infrastructure extension. Kept here (rather than
    /// resolved during DI registration) because the scheduler is a runtime singleton that must be the
    /// one instance the app actually built, not one built by a throwaway <c>IServiceCollection</c> provider.
    /// </summary>
    private void RegisterBuiltInRecurringJobs()
    {
        recurringJobScheduler.Register(new RecurringJobDefinition(
            "commerce.expire-inventory-reservations", OperationalJobTypes.ExpireInventoryReservations, TimeSpan.FromMinutes(5)));
        recurringJobScheduler.Register(new RecurringJobDefinition(
            "commerce.health-probe", OperationalJobTypes.HealthProbe, TimeSpan.FromMinutes(5), Priority: BackgroundJobPriority.Low));
        recurringJobScheduler.Register(new RecurringJobDefinition(
            "commerce.generate-operational-alerts", OperationalJobTypes.GenerateOperationalAlerts, TimeSpan.FromMinutes(5)));
    }

    /// <summary>
    /// For each definition a module registered via <see cref="IRecurringJobScheduler"/>, enqueue one
    /// occurrence unless a job of that type was already created within the definition's interval.
    /// </summary>
    private static async Task EnsureRecurringJobsAsync(
        IBackgroundJobScheduler scheduler,
        IRecurringJobRegistry recurringJobs,
        ICommerceDbContext commerceDb,
        CancellationToken cancellationToken)
    {
        var definitions = recurringJobs.GetAll();
        if (definitions.Count == 0)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var jobTypes = definitions.Select(d => d.JobType).Distinct().ToArray();
        var earliestWindow = now - definitions.Max(d => d.Interval);

        var recentTypes = (await commerceDb.OperationalJobs.AsNoTracking()
            .Where(j => j.CreatedOnUtc >= earliestWindow && jobTypes.Contains(j.JobType))
            .Select(j => new { j.JobType, j.CreatedOnUtc })
            .ToListAsync(cancellationToken))
            .GroupBy(j => j.JobType)
            .ToDictionary(g => g.Key, g => g.Max(j => j.CreatedOnUtc));

        foreach (var definition in definitions)
        {
            if (recentTypes.TryGetValue(definition.JobType, out var lastCreated) &&
                lastCreated >= now - definition.Interval)
            {
                continue;
            }

            await scheduler.EnqueueAsync(
                definition.JobType,
                new BackgroundJobOptions(definition.Queue, definition.Priority),
                cancellationToken);
        }
    }

    private async Task ProcessJobAsync(
        IServiceProvider sp,
        OperationalJob job,
        RetryPoliciesSettingsPayload retrySettings,
        CancellationToken cancellationToken)
    {
        var commerceDb = sp.GetRequiredService<ICommerceDbContext>();
        var tracked = await commerceDb.OperationalJobs
            .FirstOrDefaultAsync(j => j.Id == job.Id, cancellationToken);

        if (tracked is null)
        {
            return;
        }

        var history = BackgroundJobExecutionHistory.Start(tracked.Id, tracked.Attempts, workerState.WorkerId, tracked.CorrelationId);
        commerceDb.BackgroundJobExecutionHistory.Add(history);

        try
        {
            var registry = sp.GetRequiredService<IBackgroundJobHandlerRegistry>();
            var handler = registry.Resolve(tracked.JobType)
                ?? throw new InvalidOperationException($"No background job handler registered for job type '{tracked.JobType}'.");

            var context = new OperationalJobExecutionContext(tracked, commerceDb);
            await handler.ExecuteRawAsync(tracked.PayloadJson, context, cancellationToken);

            tracked.MarkCompleted();
            history.Complete(OperationalJobStatus.Completed, null);
            workerState.RecordSuccess();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Operational job {JobId} ({JobType}) failed", tracked.Id, tracked.JobType);
            var delay = ComputeRetryDelay(retrySettings, tracked.Attempts);
            tracked.MarkFailed(ex.Message, ex.ToString(), delay, retrySettings.DeadLetterThreshold);
            history.Complete(tracked.Status, ex.ToString());
            workerState.RecordFailure();
        }

        await commerceDb.SaveChangesAsync(cancellationToken);
    }

    private static TimeSpan ComputeRetryDelay(RetryPoliciesSettingsPayload retrySettings, int attempts)
    {
        var baseDelaySeconds = Math.Max(1, retrySettings.RetryDelaySeconds);
        if (!retrySettings.UseExponentialBackoff)
        {
            return TimeSpan.FromSeconds(baseDelaySeconds);
        }

        var exponentialSeconds = baseDelaySeconds * Math.Pow(2, Math.Max(0, attempts - 1));
        var cappedSeconds = Math.Min(exponentialSeconds, Math.Max(baseDelaySeconds, retrySettings.TimeoutSeconds));
        return TimeSpan.FromSeconds(cappedSeconds);
    }
}
