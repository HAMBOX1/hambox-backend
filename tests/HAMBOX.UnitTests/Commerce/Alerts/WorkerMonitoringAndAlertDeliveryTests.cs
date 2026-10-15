using HAMBOX.Application.BackgroundJobs;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Domain.Operations;
using HAMBOX.Modules.Commerce.Infrastructure.BackgroundJobs.Handlers;
using HAMBOX.Modules.Commerce.Infrastructure.Services;
using HAMBOX.UnitTests.Commerce.TestDoubles;
using HAMBOX.UnitTests.Suppliers.TestDoubles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace HAMBOX.UnitTests.Commerce.Alerts;

/// <summary>
/// Audit R44: a critical incident is pushed to the people who must act (not only written to a table), the scheduler
/// registers every recurring job the platform depends on, and a worker that stopped is visible from the outside.
/// </summary>
public sealed class WorkerMonitoringAndAlertDeliveryTests
{
    private sealed class RecordingNotifier : IOperationalAlertNotifier
    {
        public List<(string Code, string Severity)> Sent { get; } = [];

        public Task NotifyAsync(string code, string title, string message, string severity, CancellationToken cancellationToken = default)
        {
            Sent.Add((code, severity));
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingScheduler : IRecurringJobScheduler
    {
        public List<RecurringJobDefinition> Registered { get; } = [];

        public void Register(RecurringJobDefinition definition) => Registered.Add(definition);
    }

    private static async Task<(
        GenerateOperationalAlertsJobHandler Handler,
        HAMBOX.Modules.Commerce.Infrastructure.Persistence.CommerceDbContext Db,
        RecordingNotifier Notifier)> CreateAsync(FakeWorkerRuntimeState? worker = null)
    {
        var (commerceDb, catalogDb) = CommerceTestDbContextFactory.Create();
        var notifier = new RecordingNotifier();
        var handler = new GenerateOperationalAlertsJobHandler(
            new FakeBackgroundJobSerializer(), commerceDb, new FakeInventoryEngine(catalogDb),
            worker ?? new FakeWorkerRuntimeState(), SuppliersTestDbContextFactory.Create(), notifier);
        await Task.CompletedTask;
        return (handler, commerceDb, notifier);
    }

    private static void AddStuckJob(HAMBOX.Modules.Commerce.Infrastructure.Persistence.CommerceDbContext db)
    {
        var job = OperationalJob.Create(OperationalJobTypes.RetryDelivery);
        db.OperationalJobs.Add(job);
        db.Entry(job).Property(nameof(OperationalJob.CreatedOnUtc)).CurrentValue = DateTimeOffset.UtcNow.AddMinutes(-30);
    }

    [Fact]
    public async Task ANewCriticalAlert_IsPushedToTheNotifier_ExactlyOnce()
    {
        var (handler, db, notifier) = await CreateAsync();
        AddStuckJob(db);
        await db.SaveChangesAsync();

        await handler.HandleAsync(null, new FakeBackgroundJobContext(), CancellationToken.None);
        await handler.HandleAsync(null, new FakeBackgroundJobContext(), CancellationToken.None);

        Assert.Equal(1, notifier.Sent.Count(s => s.Code == "STUCK_JOBS"));
        Assert.All(notifier.Sent, s => Assert.Equal("Critical", s.Severity));
    }

    [Fact]
    public async Task AStaleWorkerHeartbeat_RaisesAndPushesTheWorkerAlert()
    {
        var worker = new FakeWorkerRuntimeState { LastHeartbeatUtc = DateTimeOffset.UtcNow.AddMinutes(-10) };
        var (handler, db, notifier) = await CreateAsync(worker);

        await handler.HandleAsync(null, new FakeBackgroundJobContext(), CancellationToken.None);

        Assert.True(await db.OperationalAlerts.AnyAsync(a => a.Code == "WORKER_STALE"));
        Assert.Contains(notifier.Sent, s => s.Code == "WORKER_STALE");
    }

    [Fact]
    public async Task AWarningLevelAlert_IsRecordedButNotPushed()
    {
        var (handler, db, notifier) = await CreateAsync();
        for (var i = 0; i < 5; i++)
        {
            var job = OperationalJob.Create(OperationalJobTypes.RetryDelivery);
            db.OperationalJobs.Add(job);
            db.Entry(job).Property(nameof(OperationalJob.Status)).CurrentValue = OperationalJobStatus.Failed;
        }

        await db.SaveChangesAsync();

        await handler.HandleAsync(null, new FakeBackgroundJobContext(), CancellationToken.None);

        Assert.True(await db.OperationalAlerts.AnyAsync(a => a.Code == "FAILED_JOBS_BACKLOG"));
        Assert.DoesNotContain(notifier.Sent, s => s.Code == "FAILED_JOBS_BACKLOG");
    }

    [Fact]
    public void TheWorker_RegistersEveryRecurringJobThePlatformDependsOn()
    {
        var scheduler = new RecordingScheduler();
        var worker = new OperationalJobWorker(
            new ServiceCollection().BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(),
            new FakeWorkerRuntimeState(),
            scheduler,
            jobQueueNotifier: null!,
            NullLogger<OperationalJobWorker>.Instance);

        worker.RegisterBuiltInRecurringJobs();

        var types = scheduler.Registered.Select(r => r.JobType).ToHashSet();
        Assert.Contains(OperationalJobTypes.ExpireInventoryReservations, types);
        Assert.Contains(OperationalJobTypes.GenerateOperationalAlerts, types);
        Assert.Contains(OperationalJobTypes.ReconcileDotPayments, types);
        Assert.Contains(OperationalJobTypes.ReconcileDotFawryPayments, types);
        Assert.Contains(OperationalJobTypes.ReconcileCryptomusPayments, types);
        Assert.Contains(OperationalJobTypes.ReconcileOxaPayPayments, types);
        Assert.Contains(OperationalJobTypes.RecomputeSupplierDerivedPricing, types);

        // No two registrations share a key, and every interval is positive (a zero interval would spin the worker).
        Assert.Equal(scheduler.Registered.Count, scheduler.Registered.Select(r => r.Key).Distinct().Count());
        Assert.All(scheduler.Registered, r => Assert.True(r.Interval > TimeSpan.Zero));
    }
}
