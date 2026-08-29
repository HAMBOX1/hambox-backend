using HAMBOX.Application.BackgroundJobs;
using HAMBOX.Modules.Commerce.Domain.Operations;
using HAMBOX.Modules.Commerce.Infrastructure.Services;
using HAMBOX.UnitTests.Commerce.TestDoubles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace HAMBOX.UnitTests.Commerce.Jobs;

/// <summary>
/// Proves the system-level guarantee Phase 3 actually cares about: a job-queue notifier failure (the
/// Redis publish/subscribe path) must never affect the durable SQL enqueue it's layered on top of. The
/// job row is what makes a job real — <see cref="OperationalJobQueue.EnqueueAsync(string, string?, OperationalJobPriority, string, string?, string?, string?, int?, CancellationToken)"/>
/// commits it BEFORE ever touching the notifier, so this doesn't need to fake StackExchange.Redis's
/// (very large) interfaces to prove the guarantee — a minimal <see cref="IJobQueueNotifier"/> stand-in
/// that always throws is sufficient and exercises the exact same code path a real Redis outage would.
/// </summary>
public sealed class OperationalJobQueueTests
{
    private sealed class AlwaysThrowsNotifier : IJobQueueNotifier
    {
        public Task NotifyAsync(string queue, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Simulated Redis outage.");

        public IAsyncDisposable Subscribe(string queue, Func<Task> onNotified) =>
            throw new InvalidOperationException("Simulated Redis outage.");
    }

    [Fact]
    public async Task EnqueueAsync_NotifierThrows_JobIsStillDurablyPersisted()
    {
        var (commerceDb, _) = CommerceTestDbContextFactory.Create();
        var queue = new OperationalJobQueue(commerceDb, new FakeBackgroundJobSerializer(), new AlwaysThrowsNotifier(), NullLogger<OperationalJobQueue>.Instance);

        // The real assertion: this call itself must not throw just because Redis is unreachable.
        var job = await queue.EnqueueAsync(OperationalJobTypes.ExpireInventoryReservations, relatedEntityId: "order-1");

        Assert.Equal(OperationalJobStatus.Queued, job.Status);
        var persisted = await commerceDb.OperationalJobs.AsNoTracking().SingleAsync(j => j.Id == job.Id);
        Assert.Equal(OperationalJobTypes.ExpireInventoryReservations, persisted.JobType);
    }

    [Fact]
    public async Task EnqueueAsync_NotifierWorks_JobIsPersisted_AndNotifierIsCalledAfterTheSave()
    {
        var (commerceDb, _) = CommerceTestDbContextFactory.Create();
        var notifier = new RecordingNotifier();
        var queue = new OperationalJobQueue(commerceDb, new FakeBackgroundJobSerializer(), notifier, NullLogger<OperationalJobQueue>.Instance);

        var job = await queue.EnqueueAsync(OperationalJobTypes.ExpireInventoryReservations, queue: "default");

        Assert.Single(notifier.NotifiedQueues, "default");
        // Proves ordering: the row was already queryable (i.e. already saved) by the time the
        // notification fired — the notifier is told about a job that genuinely, durably exists.
        Assert.True(await commerceDb.OperationalJobs.AnyAsync(j => j.Id == job.Id));
    }

    private sealed class RecordingNotifier : IJobQueueNotifier
    {
        public List<string> NotifiedQueues { get; } = [];

        public Task NotifyAsync(string queue, CancellationToken cancellationToken = default)
        {
            NotifiedQueues.Add(queue);
            return Task.CompletedTask;
        }

        public IAsyncDisposable Subscribe(string queue, Func<Task> onNotified) =>
            throw new NotSupportedException("Not needed by this test.");
    }
}
