using HAMBOX.Application.BackgroundJobs;
using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Domain.Enums;
using HAMBOX.Modules.Commerce.Domain.Operations;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Commerce.Infrastructure.BackgroundJobs.Handlers;

internal sealed class GenerateOperationalAlertsJobHandler(
    IBackgroundJobSerializer serializer,
    ICommerceDbContext db,
    IInventoryEngine inventory,
    IWorkerRuntimeState worker) : BackgroundJobHandlerBase<string?>(serializer)
{
    public override string JobType => OperationalJobTypes.GenerateOperationalAlerts;

    public override async Task HandleAsync(string? payload, IBackgroundJobContext context, CancellationToken cancellationToken)
    {
        var failedJobs = await db.OperationalJobs.CountAsync(
            j => j.Status == OperationalJobStatus.Failed,
            cancellationToken);
        if (failedJobs >= 5)
        {
            await OperationalAlertUpsert.UpsertAsync(
                db,
                "FAILED_JOBS_BACKLOG",
                "Failed jobs backlog",
                $"{failedJobs} operational jobs are in Failed status.",
                OperationalAlertSeverity.Warning,
                cancellationToken);
        }

        var stats = await inventory.GetStatisticsAsync(cancellationToken: cancellationToken);
        if (stats.OutOfStockVariants > 0)
        {
            await OperationalAlertUpsert.UpsertAsync(
                db,
                "LOW_STOCK",
                "Inventory stock alerts",
                $"{stats.OutOfStockVariants} variant(s) out of stock, {stats.LowStockVariants} low stock.",
                OperationalAlertSeverity.Warning,
                cancellationToken);
        }

        var failedDeliveries = await db.Orders.CountAsync(
            o => o.Status == OrderStatus.Failed || o.PaymentStatus == PaymentStatus.Failed,
            cancellationToken);
        if (failedDeliveries > 0)
        {
            await OperationalAlertUpsert.UpsertAsync(
                db,
                "FAILED_DELIVERIES",
                "Failed deliveries / payments",
                $"{failedDeliveries} order(s) marked failed.",
                OperationalAlertSeverity.Critical,
                cancellationToken);
        }

        if (worker.LastHeartbeatUtc is null ||
            (DateTimeOffset.UtcNow - worker.LastHeartbeatUtc.Value).TotalSeconds > 90)
        {
            await OperationalAlertUpsert.UpsertAsync(
                db,
                "WORKER_STALE",
                "Background worker issue",
                "Operational worker heartbeat is missing or stale.",
                OperationalAlertSeverity.Critical,
                cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
