using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Commerce.Domain.Operations;
using HAMBOX.Modules.Commerce.Infrastructure.Persistence;
using HAMBOX.Modules.Commerce.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.IntegrationTests.Commerce;

/// <summary>
/// Real SQL Server (LocalDB) coverage for the <see cref="OperationalJob"/> claim race —
/// <see cref="OperationalJobQueue.ClaimNextBatchAsync"/> is what every job type flows through
/// (including the new <c>ExecuteOrderFulfillment</c> order-execution job), so two worker instances
/// racing to claim the same queued row must never both win. Mirrors
/// <c>SupplierFulfillmentConcurrencyTests</c>' established LocalDB pattern in this repo — deliberately
/// not SQLite, for the same reason: a real database-generated <c>rowversion</c> column is required.
/// </summary>
public sealed class OperationalJobConcurrencyTests : IAsyncLifetime
{
    private readonly string _databaseName = $"HamboxOperationalJobConcurrency_{Guid.NewGuid():N}";
    private string ConnectionString => $"Server=(localdb)\\MSSQLLocalDB;Database={_databaseName};Trusted_Connection=True;TrustServerCertificate=True;";

    public async Task InitializeAsync()
    {
        await using var db = CreateContext();
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await using var db = CreateContext();
        await db.Database.EnsureDeletedAsync();
    }

    private CommerceDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<CommerceDbContext>()
            .UseSqlServer(ConnectionString, o => o.MigrationsHistoryTable("__EFMigrationsHistory", "commerce"))
            .Options;
        return new CommerceDbContext(options, new PassthroughCodeProtector());
    }

    private async Task<Guid> SeedQueuedJobAsync()
    {
        await using var db = CreateContext();
        var job = OperationalJob.Create(
            OperationalJobTypes.ExecuteOrderFulfillment,
            relatedEntityType: "Order",
            relatedEntityId: Guid.NewGuid().ToString());
        db.OperationalJobs.Add(job);
        await db.SaveChangesAsync();
        return job.Id;
    }

    /// <summary>
    /// Two separate worker instances (separate DbContexts/connections, mirroring two real API replicas
    /// or two overlapping worker ticks) both call <see cref="OperationalJobQueue.ClaimNextBatchAsync"/>
    /// against a database that has exactly one claimable job. Exactly one must come back with it in its
    /// claimed batch; the other's batch must not contain it at all — proving the
    /// <see cref="OperationalJob.RowVersion"/> concurrency token (this task's fix) actually prevents a
    /// double-claim, and that losing the race on one row degrades gracefully rather than throwing out
    /// of <c>ClaimNextBatchAsync</c> entirely.
    /// </summary>
    [Fact]
    public async Task ClaimNextBatchAsync_UnderRealSqlServerConcurrentWorkers_ExactlyOneWorkerClaimsTheJob()
    {
        var jobId = await SeedQueuedJobAsync();

        await using var dbA = CreateContext();
        await using var dbB = CreateContext();
        var queueA = new OperationalJobQueue(dbA, new FakeBackgroundJobSerializer(), new HAMBOX.Infrastructure.Services.NullJobQueueNotifier(), Microsoft.Extensions.Logging.Abstractions.NullLogger<HAMBOX.Modules.Commerce.Infrastructure.Services.OperationalJobQueue>.Instance);
        var queueB = new OperationalJobQueue(dbB, new FakeBackgroundJobSerializer(), new HAMBOX.Infrastructure.Services.NullJobQueueNotifier(), Microsoft.Extensions.Logging.Abstractions.NullLogger<HAMBOX.Modules.Commerce.Infrastructure.Services.OperationalJobQueue>.Instance);

        var claimTaskA = queueA.ClaimNextBatchAsync("worker-a", batchSize: 10);
        var claimTaskB = queueB.ClaimNextBatchAsync("worker-b", batchSize: 10);
        await Task.WhenAll(claimTaskA, claimTaskB);

        var claimedByA = claimTaskA.Result.Any(j => j.Id == jobId);
        var claimedByB = claimTaskB.Result.Any(j => j.Id == jobId);

        // Exactly one worker's batch contains the job — never both, never neither.
        Assert.True(claimedByA ^ claimedByB, $"Expected exactly one worker to claim the job (A={claimedByA}, B={claimedByB}).");

        await using var verifyDb = CreateContext();
        var persisted = await verifyDb.OperationalJobs.AsNoTracking().SingleAsync(j => j.Id == jobId);
        Assert.Equal(OperationalJobStatus.Running, persisted.Status);
        Assert.Equal(1, persisted.Attempts); // only the winning claim was ever actually persisted
        Assert.Equal(claimedByA ? "worker-a" : "worker-b", persisted.WorkerId);
    }

    /// <summary>Same race with more contenders, to reduce the chance a 2-worker race happens to avoid
    /// the interesting timing window.</summary>
    [Fact]
    public async Task ClaimNextBatchAsync_UnderRealSqlServerConcurrentWorkers_ManyWorkers_ExactlyOneWins()
    {
        var jobId = await SeedQueuedJobAsync();
        const int workerCount = 6;

        var tasks = Enumerable.Range(0, workerCount).Select(async i =>
        {
            await using var db = CreateContext();
            var queue = new OperationalJobQueue(db, new FakeBackgroundJobSerializer(), new HAMBOX.Infrastructure.Services.NullJobQueueNotifier(), Microsoft.Extensions.Logging.Abstractions.NullLogger<HAMBOX.Modules.Commerce.Infrastructure.Services.OperationalJobQueue>.Instance);
            var claimed = await queue.ClaimNextBatchAsync($"worker-{i}", batchSize: 10);
            return claimed.Any(j => j.Id == jobId);
        });

        var results = await Task.WhenAll(tasks);

        Assert.Equal(1, results.Count(claimed => claimed));
    }

    private sealed class PassthroughCodeProtector : ICodeProtector
    {
        public string Protect(string plainText) => plainText;

        public string Unprotect(string cipherText) => cipherText;
    }

    private sealed class FakeBackgroundJobSerializer : HAMBOX.Application.BackgroundJobs.IBackgroundJobSerializer
    {
        public string Serialize<TPayload>(TPayload payload) => System.Text.Json.JsonSerializer.Serialize(payload);

        public TPayload? Deserialize<TPayload>(string json) => System.Text.Json.JsonSerializer.Deserialize<TPayload>(json);
    }
}
