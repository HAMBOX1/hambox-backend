using HAMBOX.Application.BackgroundJobs;
using HAMBOX.Application.PlatformSettings;
using HAMBOX.Modules.Commerce.Domain.Operations;
using HAMBOX.Modules.Commerce.Infrastructure.Services;
using HAMBOX.UnitTests.Commerce.TestDoubles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace HAMBOX.UnitTests.Commerce.Jobs;

/// <summary>
/// Audit R31 / R38: per-order jobs are enqueued at most once while one is waiting; retry delays follow the
/// configured schedule (scope default 0 / 2 / 5 / 15 minutes) with jitter.
/// </summary>
public sealed class JobIdempotencyAndRetryPolicyTests
{
    private sealed class SilentNotifier : IJobQueueNotifier
    {
        public Task NotifyAsync(string queue, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public IAsyncDisposable Subscribe(string queue, Func<Task> onNotified) => throw new NotSupportedException();
    }

    private static (HAMBOX.Modules.Commerce.Infrastructure.Persistence.CommerceDbContext Db, OperationalJobQueue Queue) Create()
    {
        var (db, _) = CommerceTestDbContextFactory.Create();
        var queue = new OperationalJobQueue(
            db, new FakeBackgroundJobSerializer(), new SilentNotifier(), new FakeCommerceSettingsProvider(),
            NullLogger<OperationalJobQueue>.Instance);
        return (db, queue);
    }

    // ─── R31: idempotent enqueue ────────────────────────────────────────

    [Fact]
    public async Task SecondFulfillmentJobForTheSameOrder_WhileTheFirstIsQueued_IsNotCreated()
    {
        var (db, queue) = Create();

        var first = await queue.EnqueueAsync(
            OperationalJobTypes.ExecuteOrderFulfillment, relatedEntityType: "Order", relatedEntityId: "order-1", maxAttempts: 3);
        var second = await queue.EnqueueAsync(
            OperationalJobTypes.ExecuteOrderFulfillment, relatedEntityType: "Order", relatedEntityId: "order-1", maxAttempts: 3);

        Assert.Equal(first.Id, second.Id);
        Assert.Equal(1, await db.OperationalJobs.CountAsync());
    }

    [Fact]
    public async Task DifferentOrders_AndDifferentJobTypes_AreNotCollapsed()
    {
        var (db, queue) = Create();

        await queue.EnqueueAsync(OperationalJobTypes.ExecuteOrderFulfillment, relatedEntityId: "order-1", maxAttempts: 3);
        await queue.EnqueueAsync(OperationalJobTypes.ExecuteOrderFulfillment, relatedEntityId: "order-2", maxAttempts: 3);
        await queue.EnqueueAsync(OperationalJobTypes.RetryOrderFulfillment, relatedEntityId: "order-1", maxAttempts: 3);

        Assert.Equal(3, await db.OperationalJobs.CountAsync());
    }

    [Fact]
    public async Task OnceTheQueuedJobHasStarted_ANewRequestCreatesAFreshJob()
    {
        var (db, queue) = Create();
        var first = await queue.EnqueueAsync(OperationalJobTypes.ExecuteOrderFulfillment, relatedEntityId: "order-1", maxAttempts: 3);

        var tracked = await db.OperationalJobs.SingleAsync(j => j.Id == first.Id);
        tracked.MarkRunning("worker-1"); // a duplicate must not be absorbed by a job that has already read the order
        await db.SaveChangesAsync();

        var second = await queue.EnqueueAsync(OperationalJobTypes.ExecuteOrderFulfillment, relatedEntityId: "order-1", maxAttempts: 3);

        Assert.NotEqual(first.Id, second.Id);
    }

    [Fact]
    public async Task JobsWithoutAnEntity_AreNeverDeduplicated()
    {
        var (db, queue) = Create();

        await queue.EnqueueAsync(OperationalJobTypes.HealthProbe, maxAttempts: 1);
        await queue.EnqueueAsync(OperationalJobTypes.HealthProbe, maxAttempts: 1);

        Assert.Equal(2, await db.OperationalJobs.CountAsync());
    }

    // ─── R38: retry schedule + jitter ───────────────────────────────────

    private static RetryPoliciesSettingsPayload Policy(
        string? schedule = null, bool exponential = false, bool jitter = false, int delay = 30, int cap = 900) =>
        new(RetryCount: 4, RetryDelaySeconds: delay, TimeoutSeconds: cap, DeadLetterThreshold: 5,
            UseExponentialBackoff: exponential, RetryScheduleSeconds: schedule, UseJitter: jitter);

    [Theory]
    [InlineData(1, 120)]
    [InlineData(2, 300)]
    [InlineData(3, 900)]
    [InlineData(9, 900)] // past the end of the schedule keeps the last step
    public void ScheduleDrivesTheDelay_ScopeDefault_0_2_5_15_Minutes(int attemptsSoFar, int expectedSeconds)
    {
        var delay = OperationalJobWorker.ComputeRetryDelay(Policy("0,120,300,900"), attemptsSoFar);

        Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), delay);
    }

    [Fact]
    public void WithoutASchedule_TheFixedDelayIsUsed_AndExponentialBackoffStillWorks()
    {
        Assert.Equal(TimeSpan.FromSeconds(30), OperationalJobWorker.ComputeRetryDelay(Policy(), 3));
        Assert.Equal(TimeSpan.FromSeconds(120), OperationalJobWorker.ComputeRetryDelay(Policy(exponential: true), 3));
        Assert.Equal(TimeSpan.FromSeconds(900), OperationalJobWorker.ComputeRetryDelay(Policy(exponential: true, cap: 900), 20));
    }

    [Fact]
    public void Jitter_SpreadsDelaysWithinPlusMinusTwentyPercent_AndNeverGoesNegative()
    {
        var low = OperationalJobWorker.ComputeRetryDelay(Policy("0,100", jitter: true), 1, () => 0.0);
        var mid = OperationalJobWorker.ComputeRetryDelay(Policy("0,100", jitter: true), 1, () => 0.5);
        var high = OperationalJobWorker.ComputeRetryDelay(Policy("0,100", jitter: true), 1, () => 1.0);

        Assert.Equal(TimeSpan.FromSeconds(80), low);
        Assert.Equal(TimeSpan.FromSeconds(100), mid);
        Assert.Equal(TimeSpan.FromSeconds(120), high);

        Assert.Equal(TimeSpan.Zero, OperationalJobWorker.ComputeRetryDelay(Policy("0,0", jitter: true), 1, () => 1.0));
    }

    [Fact]
    public void AMalformedSchedule_FallsBackToTheFixedDelay()
    {
        var delay = OperationalJobWorker.ComputeRetryDelay(Policy("soon, later"), 2);

        Assert.Equal(TimeSpan.FromSeconds(30), delay);
    }
}
