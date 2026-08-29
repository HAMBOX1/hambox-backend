using HAMBOX.Application.BackgroundJobs;
using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Domain.Enums;
using HAMBOX.Modules.Commerce.Domain.Operations;
using HAMBOX.Modules.Suppliers.Application.Abstractions;
using HAMBOX.Modules.Suppliers.Domain.Fulfillments;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Commerce.Infrastructure.BackgroundJobs.Handlers;

internal sealed class GenerateOperationalAlertsJobHandler(
    IBackgroundJobSerializer serializer,
    ICommerceDbContext db,
    IInventoryEngine inventory,
    IWorkerRuntimeState worker,
    ISuppliersDbContext suppliersDb) : BackgroundJobHandlerBase<string?>(serializer)
{
    /// <summary>
    /// Contract-mandated default (§25.10/§41): jobs — and, per the same clause, individual order
    /// fulfillment operations — sitting active longer than this without completing are "stuck" and
    /// must alert admins, regardless of whether they're still, technically, going to succeed on their
    /// own eventually. Shared by both the generic <see cref="OperationalJob"/> check below and the
    /// order-fulfillment-specific check further down, so the two concepts never drift to different
    /// thresholds. No existing Platform Settings category models this value (RetryPolicies covers
    /// retry counts/delays/timeouts, not a stuck-detection window) — see the class remarks.
    /// </summary>
    /// <remarks>
    /// Not sourced from Platform Settings: this constant already predates this change (the
    /// <c>STUCK_JOBS</c> check below), and no operational-threshold category in
    /// <c>PlatformSettingsContracts.cs</c> models "minutes before a job/fulfillment counts as stuck" —
    /// making it configurable only for the new fulfillment check while its sibling stays hardcoded
    /// would be an inconsistency, not an improvement. If this needs to become admin-configurable,
    /// both checks should move together in one follow-up, not just this one.
    /// </remarks>
    private static readonly TimeSpan StuckJobThreshold = TimeSpan.FromMinutes(10);

    /// <summary>Caps how many stuck-fulfillment incidents one sweep pass raises alerts for — this is a
    /// cheap read-only check (no external provider calls), but still bounded so a systemic outage
    /// producing hundreds of stuck rows can't make one pass do unbounded work. Any rows beyond this
    /// are picked up on a later pass; <see cref="OperationalAlertUpsert"/> ensures already-alerted
    /// incidents are never re-raised in the meantime.</summary>
    private const int StuckFulfillmentBatchLimit = 50;

    private static readonly SupplierFulfillmentStatus[] ActiveFulfillmentStatuses =
    [
        SupplierFulfillmentStatus.Pending,
        SupplierFulfillmentStatus.Submitting,
        SupplierFulfillmentStatus.Submitted,
        SupplierFulfillmentStatus.Unknown,
    ];

    public override string JobType => OperationalJobTypes.GenerateOperationalAlerts;

    public override async Task HandleAsync(string? payload, IBackgroundJobContext context, CancellationToken cancellationToken)
    {
        var stuckCutoffUtc = DateTimeOffset.UtcNow - StuckJobThreshold;
        var stuckJobs = await db.OperationalJobs.CountAsync(
            j => (j.Status == OperationalJobStatus.Queued && j.CreatedOnUtc <= stuckCutoffUtc)
                || (j.Status == OperationalJobStatus.Running && j.StartedOnUtc != null && j.StartedOnUtc <= stuckCutoffUtc),
            cancellationToken);
        if (stuckJobs > 0)
        {
            await OperationalAlertUpsert.UpsertAsync(
                db,
                "STUCK_JOBS",
                "Stuck jobs detected",
                $"{stuckJobs} job(s) have been Queued or Running for more than {StuckJobThreshold.TotalMinutes:0} minutes without completing.",
                OperationalAlertSeverity.Critical,
                cancellationToken);
        }

        await RaiseStuckFulfillmentAlertsAsync(stuckCutoffUtc, cancellationToken);

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

    /// <summary>
    /// Detects individual <see cref="Suppliers.Domain.Fulfillments.SupplierFulfillment"/> attempts that
    /// have been sitting in a non-terminal state (Pending/Submitting/Submitted/Unknown) longer than
    /// <see cref="StuckJobThreshold"/> and raises one deduplicated alert per incident — distinct from
    /// the generic <c>STUCK_JOBS</c> check above, which only sees the job-queue row for the sweep
    /// itself (which completes normally every 2 minutes) and has no visibility into a specific order's
    /// automated-supplier-purchase attempt stalling at the provider/reconciliation level. This is a
    /// read-only detector: it never mutates <see cref="SupplierFulfillment"/> state — the existing
    /// <c>SupplierFulfillmentSweepJobHandler</c>/<c>SupplierFulfillmentService</c> remain the sole
    /// authority for retrying, reconciling, or resolving these attempts.
    /// </summary>
    private async Task RaiseStuckFulfillmentAlertsAsync(DateTimeOffset stuckCutoffUtc, CancellationToken cancellationToken)
    {
        // Mirrors the staleness signal the sweep itself already orders its reconciliation batch by
        // (see SupplierFulfillmentService.ProcessDueFulfillmentsAsync) — no new timestamp invented.
        var stuck = await suppliersDb.SupplierFulfillments.AsNoTracking()
            .Where(f => ActiveFulfillmentStatuses.Contains(f.Status))
            .Where(f => (f.LastReconciledOnUtc ?? f.CreatedOnUtc) <= stuckCutoffUtc)
            .OrderBy(f => f.LastReconciledOnUtc ?? f.CreatedOnUtc)
            .Take(StuckFulfillmentBatchLimit)
            .Select(f => new
            {
                f.Id,
                f.OrderId,
                f.OrderItemId,
                f.SupplierId,
                f.SupplierProductMappingId,
                f.Status,
                f.RequestedQuantity,
                f.DeliveredQuantity,
                f.Attempts,
                f.ProviderOrderId,
                f.HamboxReferenceId,
                f.CreatedOnUtc,
                f.LastReconciledOnUtc,
                f.FailureCategory,
                f.FailureDetail,
            })
            .ToListAsync(cancellationToken);

        if (stuck.Count == 0)
        {
            return;
        }

        var supplierIds = stuck.Select(f => f.SupplierId).Distinct().ToList();
        var mappingIds = stuck.Select(f => f.SupplierProductMappingId).Distinct().ToList();
        var orderIds = stuck.Select(f => f.OrderId).Distinct().ToList();

        var supplierNames = await suppliersDb.Suppliers.AsNoTracking()
            .Where(s => supplierIds.Contains(s.Id))
            .Select(s => new { s.Id, s.Name })
            .ToDictionaryAsync(s => s.Id, s => s.Name, cancellationToken);

        var mappingNames = await suppliersDb.SupplierProductMappings.AsNoTracking()
            .Where(m => mappingIds.Contains(m.Id))
            .Select(m => new { m.Id, m.ExternalName, m.ExternalProductId })
            .ToDictionaryAsync(m => m.Id, m => m.ExternalName ?? m.ExternalProductId, cancellationToken);

        var orders = await db.Orders.AsNoTracking()
            .Where(o => orderIds.Contains(o.Id))
            .Select(o => new { o.Id, o.OrderNumber, o.Email })
            .ToDictionaryAsync(o => o.Id, cancellationToken);

        foreach (var f in stuck)
        {
            var stuckSinceUtc = f.LastReconciledOnUtc ?? f.CreatedOnUtc;
            var stuckMinutes = (int)(DateTimeOffset.UtcNow - stuckSinceUtc).TotalMinutes;
            var order = orders.GetValueOrDefault(f.OrderId);
            var supplierName = supplierNames.GetValueOrDefault(f.SupplierId) ?? f.SupplierId.ToString();
            var productName = mappingNames.GetValueOrDefault(f.SupplierProductMappingId) ?? f.SupplierProductMappingId.ToString();
            var orderLabel = order?.OrderNumber ?? f.OrderId.ToString();

            var message =
                $"Order {orderLabel}" +
                (order is not null ? $" ({order.Email})" : string.Empty) +
                $" — fulfillment attempt has been {f.Status} for {stuckMinutes} minute(s) (threshold {StuckJobThreshold.TotalMinutes:0}). " +
                $"Supplier: {supplierName}. Product: {productName}. " +
                $"Requested/Delivered: {f.RequestedQuantity}/{f.DeliveredQuantity}. Attempts: {f.Attempts}. " +
                $"Provider order id: {f.ProviderOrderId ?? "none yet"}. " +
                (f.FailureCategory is not null ? $"Last failure category: {f.FailureCategory}. " : string.Empty) +
                (!string.IsNullOrWhiteSpace(f.FailureDetail) ? $"Last error: {f.FailureDetail}. " : string.Empty) +
                $"Reference: {f.HamboxReferenceId}.";

            await OperationalAlertUpsert.UpsertAsync(
                db,
                "STUCK_FULFILLMENT",
                $"Stuck order fulfillment — {orderLabel}",
                message,
                OperationalAlertSeverity.Critical,
                cancellationToken,
                relatedEntityType: "SupplierFulfillment",
                relatedEntityId: f.Id.ToString());
        }
    }
}
