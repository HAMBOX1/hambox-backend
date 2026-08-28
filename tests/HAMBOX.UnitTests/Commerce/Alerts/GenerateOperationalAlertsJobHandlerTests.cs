using HAMBOX.Modules.Commerce.Domain.Operations;
using HAMBOX.Modules.Commerce.Infrastructure.BackgroundJobs.Handlers;
using HAMBOX.UnitTests.Commerce.TestDoubles;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.UnitTests.Commerce.Alerts;

/// <summary>
/// Covers the recurring operational-alerts scan's Stuck Jobs check (contract §25.10: a job sitting
/// Queued or Running for more than 10 minutes must raise an admin alert). The other checks in this
/// handler (failed-jobs backlog, low stock, failed deliveries, worker staleness) predate this test
/// file and aren't re-covered here.
/// </summary>
public sealed class GenerateOperationalAlertsJobHandlerTests
{
    private static (GenerateOperationalAlertsJobHandler Handler, HAMBOX.Modules.Commerce.Infrastructure.Persistence.CommerceDbContext CommerceDb)
        CreateHandler()
    {
        var (commerceDb, catalogDb) = CommerceTestDbContextFactory.Create();
        var inventory = new FakeInventoryEngine(catalogDb);
        var worker = new FakeWorkerRuntimeState();
        var handler = new GenerateOperationalAlertsJobHandler(new FakeBackgroundJobSerializer(), commerceDb, inventory, worker);
        return (handler, commerceDb);
    }

    private static OperationalJob AddJob(
        HAMBOX.Modules.Commerce.Infrastructure.Persistence.CommerceDbContext db,
        OperationalJobStatus status,
        DateTimeOffset createdOnUtc,
        DateTimeOffset? startedOnUtc = null)
    {
        var job = OperationalJob.Create(OperationalJobTypes.RetryDelivery);
        db.OperationalJobs.Add(job);

        // The domain model only ever stamps these as "now" via Create()/MarkRunning() — backdating
        // is test-only setup for "this job has been sitting for N minutes", not a real mutation path.
        db.Entry(job).Property(nameof(OperationalJob.CreatedOnUtc)).CurrentValue = createdOnUtc;
        db.Entry(job).Property(nameof(OperationalJob.Status)).CurrentValue = status;
        if (startedOnUtc.HasValue)
        {
            db.Entry(job).Property(nameof(OperationalJob.StartedOnUtc)).CurrentValue = startedOnUtc.Value;
        }

        return job;
    }

    [Fact]
    public async Task Handle_JobQueuedOverTenMinutes_RaisesStuckJobsAlert()
    {
        var (handler, commerceDb) = CreateHandler();
        AddJob(commerceDb, OperationalJobStatus.Queued, DateTimeOffset.UtcNow.AddMinutes(-11));
        await commerceDb.SaveChangesAsync(CancellationToken.None);

        await handler.HandleAsync(null, new FakeBackgroundJobContext(), CancellationToken.None);

        var alert = await commerceDb.OperationalAlerts.AsNoTracking().SingleOrDefaultAsync(a => a.Code == "STUCK_JOBS");
        Assert.NotNull(alert);
        Assert.Equal(OperationalAlertSeverity.Critical, alert!.Severity);
    }

    [Fact]
    public async Task Handle_JobRunningOverTenMinutes_RaisesStuckJobsAlert()
    {
        var (handler, commerceDb) = CreateHandler();
        AddJob(
            commerceDb,
            OperationalJobStatus.Running,
            createdOnUtc: DateTimeOffset.UtcNow.AddMinutes(-20),
            startedOnUtc: DateTimeOffset.UtcNow.AddMinutes(-15));
        await commerceDb.SaveChangesAsync(CancellationToken.None);

        await handler.HandleAsync(null, new FakeBackgroundJobContext(), CancellationToken.None);

        Assert.True(await commerceDb.OperationalAlerts.AnyAsync(a => a.Code == "STUCK_JOBS"));
    }

    [Fact]
    public async Task Handle_JobQueuedUnderTenMinutes_DoesNotRaiseStuckJobsAlert()
    {
        var (handler, commerceDb) = CreateHandler();
        AddJob(commerceDb, OperationalJobStatus.Queued, DateTimeOffset.UtcNow.AddMinutes(-2));
        await commerceDb.SaveChangesAsync(CancellationToken.None);

        await handler.HandleAsync(null, new FakeBackgroundJobContext(), CancellationToken.None);

        Assert.False(await commerceDb.OperationalAlerts.AnyAsync(a => a.Code == "STUCK_JOBS"));
    }

    [Fact]
    public async Task Handle_JobCompletedLongAgo_IsNeverConsideredStuck()
    {
        var (handler, commerceDb) = CreateHandler();
        AddJob(commerceDb, OperationalJobStatus.Completed, DateTimeOffset.UtcNow.AddHours(-2));
        await commerceDb.SaveChangesAsync(CancellationToken.None);

        await handler.HandleAsync(null, new FakeBackgroundJobContext(), CancellationToken.None);

        Assert.False(await commerceDb.OperationalAlerts.AnyAsync(a => a.Code == "STUCK_JOBS"));
    }

    [Fact]
    public async Task Handle_RepeatedExecutionWithinWindow_DoesNotDuplicateAlert()
    {
        var (handler, commerceDb) = CreateHandler();
        AddJob(commerceDb, OperationalJobStatus.Queued, DateTimeOffset.UtcNow.AddMinutes(-30));
        await commerceDb.SaveChangesAsync(CancellationToken.None);

        await handler.HandleAsync(null, new FakeBackgroundJobContext(), CancellationToken.None);
        // Simulates the next 5-minute recurring pass while the same job is still stuck.
        await handler.HandleAsync(null, new FakeBackgroundJobContext(), CancellationToken.None);

        Assert.Equal(1, await commerceDb.OperationalAlerts.CountAsync(a => a.Code == "STUCK_JOBS"));
    }
}
