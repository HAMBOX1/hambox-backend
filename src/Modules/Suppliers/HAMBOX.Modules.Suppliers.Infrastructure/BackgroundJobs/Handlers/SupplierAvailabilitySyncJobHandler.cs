using HAMBOX.Application.BackgroundJobs;
using HAMBOX.Modules.Suppliers.Application.Abstractions;
using HAMBOX.Modules.Suppliers.Application.BackgroundJobs;
using Microsoft.Extensions.Logging;

namespace HAMBOX.Modules.Suppliers.Infrastructure.BackgroundJobs.Handlers;

/// <summary>
/// Thin adapter onto the shared background-job engine — mirrors <c>SupplierFulfillmentSweepJobHandler</c>
/// exactly. All the actual logic lives in <see cref="ISupplierAvailabilityService.SyncAllEnabledSuppliersAsync"/>,
/// which needs no <see cref="IBackgroundJobContext"/> and is independently unit-testable.
/// </summary>
/// <remarks>
/// Scheduled from <c>Program.cs</c> via <c>IRecurringJobScheduler</c> (key <c>"suppliers.availability-sync"</c>)
/// at an interval read from <c>SupplierAvailability:SyncIntervalMinutes</c> (default 5) — the same shared
/// <c>OperationalJobWorker</c> every other module's recurring job already runs through, no new hosted
/// service.
/// </remarks>
internal sealed class SupplierAvailabilitySyncJobHandler(
    IBackgroundJobSerializer serializer,
    ISupplierAvailabilityService availabilityService,
    ILogger<SupplierAvailabilitySyncJobHandler> logger) : BackgroundJobHandlerBase<string?>(serializer)
{
    public override string JobType => SupplierAvailabilityJobTypes.Sync;

    public override async Task HandleAsync(string? payload, IBackgroundJobContext context, CancellationToken cancellationToken)
    {
        var results = await availabilityService.SyncAllEnabledSuppliersAsync(cancellationToken);

        logger.LogInformation(
            "Supplier availability sync tick: {SupplierCount} supplier(s) synced, {FailedCount} failed.",
            results.Count, results.Count(r => !r.IsSuccess));
    }
}
