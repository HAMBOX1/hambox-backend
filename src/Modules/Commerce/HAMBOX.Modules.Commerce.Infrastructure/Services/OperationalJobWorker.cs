using System.Text.Json;
using HAMBOX.Application.Abstractions;
using HAMBOX.Application.PlatformSettings;
using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Catalog.Domain.Enums;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Services;
using HAMBOX.Modules.Commerce.Domain.Enums;
using HAMBOX.Modules.Commerce.Domain.Operations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HAMBOX.Modules.Commerce.Infrastructure.Services;

internal sealed class OperationalJobWorker(
    IServiceScopeFactory scopeFactory,
    IWorkerRuntimeState workerState,
    ILogger<OperationalJobWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        workerState.MarkStarted();
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
                var commerceDb = scope.ServiceProvider.GetRequiredService<ICommerceDbContext>();

                var queueSettings = await platformSettings.GetAsync<QueueSettingsPayload>(
                    PlatformSettingsCategoryKeys.Queue,
                    stoppingToken);
                delaySeconds = Math.Clamp(queueSettings.WorkerIntervalSeconds, 5, 300);

                var retrySettings = await platformSettings.GetAsync<RetryPoliciesSettingsPayload>(
                    PlatformSettingsCategoryKeys.RetryPolicies,
                    stoppingToken);

                await EnsurePeriodicJobsAsync(queue, commerceDb, stoppingToken);

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

    private static async Task EnsurePeriodicJobsAsync(
        IOperationalJobQueue queue,
        ICommerceDbContext commerceDb,
        CancellationToken cancellationToken)
    {
        var since = DateTimeOffset.UtcNow.AddMinutes(-5);
        var recentTypes = await commerceDb.OperationalJobs.AsNoTracking()
            .Where(j => j.CreatedOnUtc >= since
                        && (j.JobType == OperationalJobTypes.ExpireInventoryReservations
                            || j.JobType == OperationalJobTypes.HealthProbe
                            || j.JobType == OperationalJobTypes.GenerateOperationalAlerts))
            .Select(j => j.JobType)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (!recentTypes.Contains(OperationalJobTypes.ExpireInventoryReservations))
        {
            await queue.EnqueueAsync(
                OperationalJobTypes.ExpireInventoryReservations,
                priority: OperationalJobPriority.Normal,
                cancellationToken: cancellationToken);
        }

        if (!recentTypes.Contains(OperationalJobTypes.HealthProbe))
        {
            await queue.EnqueueAsync(
                OperationalJobTypes.HealthProbe,
                priority: OperationalJobPriority.Low,
                cancellationToken: cancellationToken);
        }

        if (!recentTypes.Contains(OperationalJobTypes.GenerateOperationalAlerts))
        {
            await queue.EnqueueAsync(
                OperationalJobTypes.GenerateOperationalAlerts,
                priority: OperationalJobPriority.Normal,
                cancellationToken: cancellationToken);
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

        try
        {
            await ExecuteJobTypeAsync(sp, tracked, cancellationToken);
            tracked.MarkCompleted();
            workerState.RecordSuccess();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Operational job {JobId} ({JobType}) failed", tracked.Id, tracked.JobType);
            var delay = TimeSpan.FromSeconds(Math.Max(1, retrySettings.RetryDelaySeconds));
            tracked.MarkFailed(ex.Message, ex.ToString(), delay);
            workerState.RecordFailure();
        }

        await commerceDb.SaveChangesAsync(cancellationToken);
    }

    private static async Task ExecuteJobTypeAsync(
        IServiceProvider sp,
        OperationalJob job,
        CancellationToken cancellationToken)
    {
        switch (job.JobType)
        {
            case OperationalJobTypes.ExpireInventoryReservations:
            {
                var engine = sp.GetRequiredService<IInventoryEngine>();
                await engine.ExpireStaleReservationsAsync(cancellationToken);
                break;
            }
            case OperationalJobTypes.RetryOrderFulfillment:
            case OperationalJobTypes.RetryDelivery:
            {
                await RetryOrderAsync(sp, job, cancellationToken);
                break;
            }
            case OperationalJobTypes.HealthProbe:
            {
                var health = sp.GetRequiredService<ISystemHealthService>();
                var commerceDb = sp.GetRequiredService<ICommerceDbContext>();
                var result = await health.GetHealthAsync(cancellationToken);
                foreach (var component in result.Components.Where(c => c.Status is "Unhealthy" or "Degraded"))
                {
                    var code = $"HEALTH_{component.Name.Replace(' ', '_').ToUpperInvariant()}";
                    var exists = await commerceDb.OperationalAlerts.AnyAsync(
                        a => a.Code == code && !a.IsAcknowledged && a.CreatedOnUtc >= DateTimeOffset.UtcNow.AddHours(-1),
                        cancellationToken);
                    if (!exists)
                    {
                        commerceDb.OperationalAlerts.Add(OperationalAlert.Create(
                            code,
                            $"{component.Name} {component.Status}",
                            component.Detail ?? $"{component.Name} reported {component.Status}.",
                            component.Status == "Unhealthy"
                                ? OperationalAlertSeverity.Critical
                                : OperationalAlertSeverity.Warning));
                    }
                }

                await commerceDb.SaveChangesAsync(cancellationToken);
                break;
            }
            case OperationalJobTypes.GenerateOperationalAlerts:
            {
                await GenerateAlertsAsync(sp, cancellationToken);
                break;
            }
            case OperationalJobTypes.SupplierHealthCheck:
            {
                await SupplierHealthAsync(sp, cancellationToken);
                break;
            }
            default:
                throw new InvalidOperationException($"Unknown operational job type '{job.JobType}'.");
        }
    }

    private static async Task RetryOrderAsync(
        IServiceProvider sp,
        OperationalJob job,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(job.RelatedEntityId, out var orderId))
        {
            if (!string.IsNullOrWhiteSpace(job.PayloadJson))
            {
                using var doc = JsonDocument.Parse(job.PayloadJson);
                if (doc.RootElement.TryGetProperty("orderId", out var prop) &&
                    prop.TryGetGuid(out var payloadId))
                {
                    orderId = payloadId;
                }
            }
        }

        if (orderId == Guid.Empty)
        {
            throw new InvalidOperationException("Retry job is missing orderId.");
        }

        var commerceDb = sp.GetRequiredService<ICommerceDbContext>();
        var fulfillment = sp.GetRequiredService<OrderFulfillmentService>();
        var order = await commerceDb.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken)
            ?? throw new InvalidOperationException($"Order {orderId} not found.");

        var result = await fulfillment.FulfillMissingAsync(order, cancellationToken);
        if (result.CodesDelivered == 0)
        {
            throw new InvalidOperationException("No codes were delivered on retry.");
        }

        await commerceDb.SaveChangesAsync(cancellationToken);
    }

    private static async Task GenerateAlertsAsync(IServiceProvider sp, CancellationToken cancellationToken)
    {
        var commerceDb = sp.GetRequiredService<ICommerceDbContext>();
        var catalogDb = sp.GetRequiredService<ICatalogDbContext>();
        var inventory = sp.GetRequiredService<IInventoryEngine>();
        var worker = sp.GetRequiredService<IWorkerRuntimeState>();

        var failedJobs = await commerceDb.OperationalJobs.CountAsync(
            j => j.Status == OperationalJobStatus.Failed,
            cancellationToken);
        if (failedJobs >= 5)
        {
            await UpsertAlertAsync(
                commerceDb,
                "FAILED_JOBS_BACKLOG",
                "Failed jobs backlog",
                $"{failedJobs} operational jobs are in Failed status.",
                OperationalAlertSeverity.Warning,
                cancellationToken);
        }

        var stats = await inventory.GetStatisticsAsync(cancellationToken: cancellationToken);
        if (stats.OutOfStockVariants > 0)
        {
            await UpsertAlertAsync(
                commerceDb,
                "LOW_STOCK",
                "Inventory stock alerts",
                $"{stats.OutOfStockVariants} variant(s) out of stock, {stats.LowStockVariants} low stock.",
                OperationalAlertSeverity.Warning,
                cancellationToken);
        }

        var failedDeliveries = await commerceDb.Orders.CountAsync(
            o => o.Status == OrderStatus.Failed || o.PaymentStatus == PaymentStatus.Failed,
            cancellationToken);
        if (failedDeliveries > 0)
        {
            await UpsertAlertAsync(
                commerceDb,
                "FAILED_DELIVERIES",
                "Failed deliveries / payments",
                $"{failedDeliveries} order(s) marked failed.",
                OperationalAlertSeverity.Critical,
                cancellationToken);
        }

        if (worker.LastHeartbeatUtc is null ||
            (DateTimeOffset.UtcNow - worker.LastHeartbeatUtc.Value).TotalSeconds > 90)
        {
            await UpsertAlertAsync(
                commerceDb,
                "WORKER_STALE",
                "Background worker issue",
                "Operational worker heartbeat is missing or stale.",
                OperationalAlertSeverity.Critical,
                cancellationToken);
        }

        _ = catalogDb;
        await commerceDb.SaveChangesAsync(cancellationToken);
    }

    private static async Task SupplierHealthAsync(IServiceProvider sp, CancellationToken cancellationToken)
    {
        var catalogDb = sp.GetRequiredService<ICatalogDbContext>();
        var commerceDb = sp.GetRequiredService<ICommerceDbContext>();

        var inactive = await catalogDb.InventorySuppliers.AsNoTracking()
            .CountAsync(s => !s.IsDeleted && s.Status != SupplierStatus.Active, cancellationToken);

        if (inactive > 0)
        {
            await UpsertAlertAsync(
                commerceDb,
                "SUPPLIER_INACTIVE",
                "Inactive suppliers",
                $"{inactive} supplier(s) are not Active.",
                OperationalAlertSeverity.Info,
                cancellationToken);
            await commerceDb.SaveChangesAsync(cancellationToken);
        }
    }

    private static async Task UpsertAlertAsync(
        ICommerceDbContext commerceDb,
        string code,
        string title,
        string message,
        OperationalAlertSeverity severity,
        CancellationToken cancellationToken)
    {
        var exists = await commerceDb.OperationalAlerts.AnyAsync(
            a => a.Code == code && !a.IsAcknowledged && a.CreatedOnUtc >= DateTimeOffset.UtcNow.AddHours(-6),
            cancellationToken);

        if (!exists)
        {
            commerceDb.OperationalAlerts.Add(OperationalAlert.Create(code, title, message, severity));
        }
    }
}
