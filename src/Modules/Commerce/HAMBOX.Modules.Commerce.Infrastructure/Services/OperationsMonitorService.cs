using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Catalog.Domain.Enums;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Contracts.Operations;
using HAMBOX.Modules.Commerce.Application.Services;
using HAMBOX.Modules.Commerce.Domain.Account;
using HAMBOX.Modules.Commerce.Domain.Enums;
using HAMBOX.Modules.Commerce.Domain.Operations;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Commerce.Infrastructure.Services;

internal sealed class OperationsMonitorService(
    ICommerceDbContext commerceDb,
    ICatalogDbContext catalogDb,
    IInventoryEngine inventoryEngine,
    IWorkerRuntimeState workerState) : IOperationsMonitorService
{
    public async Task<OperationsDashboardDto> GetDashboardAsync(CancellationToken cancellationToken = default)
    {
        var jobGroups = await commerceDb.OperationalJobs.AsNoTracking()
            .GroupBy(j => j.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        int CountJobs(OperationalJobStatus status) =>
            jobGroups.FirstOrDefault(g => g.Status == status)?.Count ?? 0;

        var jobs = new OperationsJobMetricsDto(
            CountJobs(OperationalJobStatus.Queued),
            CountJobs(OperationalJobStatus.Running),
            CountJobs(OperationalJobStatus.Completed),
            CountJobs(OperationalJobStatus.Failed),
            CountJobs(OperationalJobStatus.Retrying),
            CountJobs(OperationalJobStatus.Cancelled),
            jobGroups.Sum(g => g.Count));

        var orders = await commerceDb.Orders.AsNoTracking()
            .Include(o => o.Items)
            .ToListAsync(cancellationToken);

        var licenseKeys = await commerceDb.OrderLicenseKeys.AsNoTracking()
            .ToListAsync(cancellationToken);

        var keysByOrder = licenseKeys
            .GroupBy(k => k.OrderId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<OrderLicenseKey>)g.ToList());

        var waiting = 0;
        var delivered = 0;
        var failedOrders = 0;
        var processing = 0;

        foreach (var order in orders)
        {
            if (order.Status == OrderStatus.Failed || order.PaymentStatus == PaymentStatus.Failed)
            {
                failedOrders++;
            }

            if (order.Status is OrderStatus.Processing or OrderStatus.Pending)
            {
                processing++;
            }

            var delivery = AdminOrderMapper.ResolveDeliveryStatus(
                order,
                keysByOrder.TryGetValue(order.Id, out var keys) ? keys : Array.Empty<OrderLicenseKey>());

            if (delivery is "Awaiting Delivery" or "Partially Delivered")
            {
                waiting++;
            }
            else if (delivery == "Delivered")
            {
                delivered++;
            }
        }

        var orderMetrics = new OperationsOrderMetricsDto(waiting, delivered, failedOrders, processing);

        var activeReservations = await catalogDb.InventoryReservations.AsNoTracking()
            .CountAsync(r => r.IsActive, cancellationToken);
        var expiredReservations = await catalogDb.InventoryReservations.AsNoTracking()
            .CountAsync(r => !r.IsActive && r.ReleasedOnUtc != null, cancellationToken);

        var inventoryStats = await inventoryEngine.GetStatisticsAsync(cancellationToken: cancellationToken);
        var inventoryMetrics = new OperationsInventoryMetricsDto(
            activeReservations,
            expiredReservations,
            inventoryStats.LowStockVariants,
            inventoryStats.OutOfStockVariants);

        var suppliers = await catalogDb.InventorySuppliers.AsNoTracking()
            .Where(s => !s.IsDeleted)
            .GroupBy(s => s.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var activeSuppliers = suppliers.FirstOrDefault(s => s.Status == SupplierStatus.Active)?.Count ?? 0;
        var totalSuppliers = suppliers.Sum(s => s.Count);
        var supplierMetrics = new OperationsSupplierMetricsDto(
            activeSuppliers,
            totalSuppliers - activeSuppliers,
            totalSuppliers);

        var hourAgo = DateTimeOffset.UtcNow.AddHours(-1);
        var dayAgo = DateTimeOffset.UtcNow.AddHours(-24);

        var apiLastHour = await commerceDb.ApiRequestLogs.AsNoTracking()
            .Where(l => l.TimestampUtc >= hourAgo)
            .Select(l => new { l.StatusCode, l.DurationMs })
            .ToListAsync(cancellationToken);

        var errorsLast24h = await commerceDb.ApiRequestLogs.AsNoTracking()
            .CountAsync(l => l.TimestampUtc >= dayAgo && l.StatusCode >= 500, cancellationToken);

        var apiMetrics = new OperationsApiMetricsDto(
            apiLastHour.Count(l => l.StatusCode >= 500),
            errorsLast24h,
            apiLastHour.Count,
            apiLastHour.Count > 0 ? apiLastHour.Average(l => (double)l.DurationMs) : 0);

        var alerts = await commerceDb.OperationalAlerts.AsNoTracking()
            .OrderByDescending(a => a.CreatedOnUtc)
            .Take(20)
            .Select(a => new OperationalAlertDto(
                a.Id,
                a.Code,
                a.Title,
                a.Message,
                a.Severity.ToString(),
                a.RelatedEntityType,
                a.RelatedEntityId,
                a.CreatedOnUtc,
                a.IsAcknowledged,
                a.AcknowledgedOnUtc,
                a.AcknowledgedByUserId))
            .ToListAsync(cancellationToken);

        var completedJobs = await commerceDb.OperationalJobs.AsNoTracking()
            .Where(j => j.Status == OperationalJobStatus.Completed
                        && j.StartedOnUtc != null
                        && j.FinishedOnUtc != null)
            .Select(j => new { j.StartedOnUtc, j.FinishedOnUtc })
            .Take(500)
            .ToListAsync(cancellationToken);

        double? avgJobSeconds = completedJobs.Count > 0
            ? completedJobs.Average(j => (j.FinishedOnUtc!.Value - j.StartedOnUtc!.Value).TotalSeconds)
            : null;

        double? avgDeliverySeconds = null;
        var deliveredPairs = orders
            .Where(o => keysByOrder.TryGetValue(o.Id, out var keys) && keys.Count > 0)
            .Select(o =>
            {
                var firstKey = keysByOrder[o.Id].OrderBy(k => k.CreatedOnUtc).First();
                return (firstKey.CreatedOnUtc - o.CreatedOnUtc).TotalSeconds;
            })
            .Where(s => s >= 0)
            .Take(500)
            .ToList();

        if (deliveredPairs.Count > 0)
        {
            avgDeliverySeconds = deliveredPairs.Average();
        }

        var heartbeatAge = workerState.LastHeartbeatUtc.HasValue
            ? (int?)Math.Max(0, (DateTimeOffset.UtcNow - workerState.LastHeartbeatUtc.Value).TotalSeconds)
            : null;

        var worker = new WorkerRuntimeStateDto(
            workerState.WorkerId,
            workerState.IsRunning,
            workerState.LastHeartbeatUtc,
            workerState.LastTickUtc,
            workerState.ProcessedCount,
            workerState.SucceededCount,
            workerState.FailedCount,
            heartbeatAge);

        return new OperationsDashboardDto(
            jobs,
            orderMetrics,
            inventoryMetrics,
            supplierMetrics,
            apiMetrics,
            alerts,
            worker,
            new OperationsTimingMetricsDto(avgJobSeconds, avgDeliverySeconds));
    }
}
