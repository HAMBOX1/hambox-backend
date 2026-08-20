using HAMBOX.Application.BackgroundJobs;
using HAMBOX.Modules.Suppliers.Application.Abstractions;
using HAMBOX.Modules.Suppliers.Application.BackgroundJobs;
using Microsoft.Extensions.Logging;

namespace HAMBOX.Modules.Suppliers.Infrastructure.BackgroundJobs.Handlers;

/// <summary>
/// Thin adapter onto the shared background-job engine — all the actual logic (claim, purchase,
/// reconcile, follow-up creation) lives in <see cref="ISupplierFulfillmentService.ProcessDueFulfillmentsAsync"/>,
/// which needs no <see cref="IBackgroundJobContext"/> and is independently unit-testable. This class
/// exists only so the shared worker has something to dispatch <see cref="SupplierFulfillmentJobTypes.Sweep"/>
/// to.
/// </summary>
/// <remarks>
/// Scheduled every 2 minutes via <c>IRecurringJobScheduler</c>, registered from <c>Program.cs</c>
/// (key <c>"suppliers.fulfillment-sweep"</c>) — the same pattern already used there for Identity's
/// security-cleanup jobs, and dispatched by the same shared <c>OperationalJobWorker</c> in
/// Commerce.Infrastructure that every other module's background job already runs through. No change
/// to Commerce itself was needed.
/// </remarks>
internal sealed class SupplierFulfillmentSweepJobHandler(
    IBackgroundJobSerializer serializer,
    ISupplierFulfillmentService fulfillmentService,
    ILogger<SupplierFulfillmentSweepJobHandler> logger) : BackgroundJobHandlerBase<string?>(serializer)
{
    private const int BatchSize = 25;

    public override string JobType => SupplierFulfillmentJobTypes.Sweep;

    public override async Task HandleAsync(string? payload, IBackgroundJobContext context, CancellationToken cancellationToken)
    {
        var result = await fulfillmentService.ProcessDueFulfillmentsAsync(BatchSize, cancellationToken);

        logger.LogInformation(
            "Supplier fulfillment sweep: {Examined} examined, {Submitted} submitted, {Reconciled} reconciled, " +
            "{FollowUps} follow-ups created, {LostRace} claim races lost, {Errors} errors.",
            result.AttemptsExamined, result.Submitted, result.Reconciled, result.FollowUpsCreated, result.ClaimRaceLosses, result.Errors);
    }
}
