using HAMBOX.Modules.Commerce.Domain.Operations;
using HAMBOX.Modules.Commerce.Infrastructure.BackgroundJobs.Handlers;
using HAMBOX.Modules.Suppliers.Domain.Fulfillments;
using HAMBOX.Modules.Suppliers.Domain.Suppliers;
using HAMBOX.Modules.Suppliers.Infrastructure.Persistence;
using HAMBOX.UnitTests.Commerce.TestDoubles;
using HAMBOX.UnitTests.Suppliers.TestDoubles;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.UnitTests.Commerce.Alerts;

/// <summary>
/// Covers the recurring operational-alerts scan's Stuck Jobs check (contract §25.10: a job sitting
/// Queued or Running for more than 10 minutes must raise an admin alert) and the Stuck Order
/// Fulfillment check added alongside it (same contract clause, applied to individual
/// <see cref="SupplierFulfillment"/> attempts rather than the job queue itself). The other checks in
/// this handler (failed-jobs backlog, low stock, failed deliveries, worker staleness) predate this
/// test file and aren't re-covered here.
/// </summary>
public sealed class GenerateOperationalAlertsJobHandlerTests
{
    private static (GenerateOperationalAlertsJobHandler Handler, HAMBOX.Modules.Commerce.Infrastructure.Persistence.CommerceDbContext CommerceDb, SuppliersDbContext SuppliersDb)
        CreateHandler()
    {
        var (commerceDb, catalogDb) = CommerceTestDbContextFactory.Create();
        var suppliersDb = SuppliersTestDbContextFactory.Create();
        var inventory = new FakeInventoryEngine(catalogDb);
        var worker = new FakeWorkerRuntimeState();
        var handler = new GenerateOperationalAlertsJobHandler(new FakeBackgroundJobSerializer(), commerceDb, inventory, worker, suppliersDb);
        return (handler, commerceDb, suppliersDb);
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
        var (handler, commerceDb, _) = CreateHandler();
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
        var (handler, commerceDb, _) = CreateHandler();
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
        var (handler, commerceDb, _) = CreateHandler();
        AddJob(commerceDb, OperationalJobStatus.Queued, DateTimeOffset.UtcNow.AddMinutes(-2));
        await commerceDb.SaveChangesAsync(CancellationToken.None);

        await handler.HandleAsync(null, new FakeBackgroundJobContext(), CancellationToken.None);

        Assert.False(await commerceDb.OperationalAlerts.AnyAsync(a => a.Code == "STUCK_JOBS"));
    }

    [Fact]
    public async Task Handle_JobCompletedLongAgo_IsNeverConsideredStuck()
    {
        var (handler, commerceDb, _) = CreateHandler();
        AddJob(commerceDb, OperationalJobStatus.Completed, DateTimeOffset.UtcNow.AddHours(-2));
        await commerceDb.SaveChangesAsync(CancellationToken.None);

        await handler.HandleAsync(null, new FakeBackgroundJobContext(), CancellationToken.None);

        Assert.False(await commerceDb.OperationalAlerts.AnyAsync(a => a.Code == "STUCK_JOBS"));
    }

    [Fact]
    public async Task Handle_RepeatedExecutionWithinWindow_DoesNotDuplicateAlert()
    {
        var (handler, commerceDb, _) = CreateHandler();
        AddJob(commerceDb, OperationalJobStatus.Queued, DateTimeOffset.UtcNow.AddMinutes(-30));
        await commerceDb.SaveChangesAsync(CancellationToken.None);

        await handler.HandleAsync(null, new FakeBackgroundJobContext(), CancellationToken.None);
        // Simulates the next 5-minute recurring pass while the same job is still stuck.
        await handler.HandleAsync(null, new FakeBackgroundJobContext(), CancellationToken.None);

        Assert.Equal(1, await commerceDb.OperationalAlerts.CountAsync(a => a.Code == "STUCK_JOBS"));
    }

    // ──── Stuck Order Fulfillment ──────────────────────────────────────────────────────────────────

    private static (Supplier Supplier, SupplierProductMapping Mapping) SeedSupplier(SuppliersDbContext db, string? apiKey = null)
    {
        var supplier = Supplier.Create("Test Supplier", $"SUP-{Guid.NewGuid():N}", "Fake", SupplierAuthenticationType.None, null, 0);
        if (apiKey is not null)
        {
            supplier.UpdateCredentials(apiKey, "secret-api-secret-value", null, null, null, null);
        }

        db.Suppliers.Add(supplier);

        var mapping = SupplierProductMapping.Create(supplier.Id, Guid.NewGuid(), "EXT-1", null, "Test Product", 5m, "USD", 0);
        db.SupplierProductMappings.Add(mapping);

        return (supplier, mapping);
    }

    /// <summary>Creates a fulfillment attempt and backdates it into whatever status/staleness a test
    /// needs — mirrors <see cref="AddJob"/>'s test-only backdoor, since the real state machine's public
    /// methods (Claim/MarkSubmitted/...) don't expose "as if this happened N minutes ago".</summary>
    private static SupplierFulfillment AddFulfillment(
        SuppliersDbContext db,
        Guid supplierId,
        Guid mappingId,
        SupplierFulfillmentStatus status,
        DateTimeOffset createdOnUtc,
        DateTimeOffset? lastReconciledOnUtc = null,
        Guid? orderId = null,
        int attempts = 1)
    {
        var fulfillment = SupplierFulfillment.Create(orderId ?? Guid.NewGuid(), Guid.NewGuid(), supplierId, mappingId, 3);
        db.SupplierFulfillments.Add(fulfillment);

        db.Entry(fulfillment).Property(nameof(SupplierFulfillment.CreatedOnUtc)).CurrentValue = createdOnUtc;
        db.Entry(fulfillment).Property(nameof(SupplierFulfillment.Status)).CurrentValue = status;
        db.Entry(fulfillment).Property(nameof(SupplierFulfillment.Attempts)).CurrentValue = attempts;
        if (lastReconciledOnUtc.HasValue)
        {
            db.Entry(fulfillment).Property(nameof(SupplierFulfillment.LastReconciledOnUtc)).CurrentValue = lastReconciledOnUtc.Value;
        }

        return fulfillment;
    }

    [Fact]
    public async Task Handle_FulfillmentActiveUnderTenMinutes_DoesNotRaiseStuckFulfillmentAlert()
    {
        var (handler, commerceDb, suppliersDb) = CreateHandler();
        var (supplier, mapping) = SeedSupplier(suppliersDb);
        AddFulfillment(suppliersDb, supplier.Id, mapping.Id, SupplierFulfillmentStatus.Submitted, DateTimeOffset.UtcNow.AddMinutes(-2));
        await suppliersDb.SaveChangesAsync(CancellationToken.None);

        await handler.HandleAsync(null, new FakeBackgroundJobContext(), CancellationToken.None);

        Assert.False(await commerceDb.OperationalAlerts.AnyAsync(a => a.Code == "STUCK_FULFILLMENT"));
    }

    [Fact]
    public async Task Handle_FulfillmentActiveOverTenMinutes_RaisesStuckFulfillmentAlert()
    {
        var (handler, commerceDb, suppliersDb) = CreateHandler();
        var (supplier, mapping) = SeedSupplier(suppliersDb);
        AddFulfillment(suppliersDb, supplier.Id, mapping.Id, SupplierFulfillmentStatus.Submitted, DateTimeOffset.UtcNow.AddMinutes(-11));
        await suppliersDb.SaveChangesAsync(CancellationToken.None);

        await handler.HandleAsync(null, new FakeBackgroundJobContext(), CancellationToken.None);

        var alert = await commerceDb.OperationalAlerts.AsNoTracking().SingleOrDefaultAsync(a => a.Code == "STUCK_FULFILLMENT");
        Assert.NotNull(alert);
        Assert.Equal(OperationalAlertSeverity.Critical, alert!.Severity);
    }

    [Theory]
    [InlineData(SupplierFulfillmentStatus.Succeeded)]
    [InlineData(SupplierFulfillmentStatus.Failed)]
    [InlineData(SupplierFulfillmentStatus.PartialFailed)]
    public async Task Handle_TerminalFulfillment_EvenIfOld_IsNeverConsideredStuck(SupplierFulfillmentStatus terminalStatus)
    {
        var (handler, commerceDb, suppliersDb) = CreateHandler();
        var (supplier, mapping) = SeedSupplier(suppliersDb);
        // "Historical records that were already resolved" — days old, well past the threshold, but
        // terminal, so the background monitor must never touch it.
        AddFulfillment(suppliersDb, supplier.Id, mapping.Id, terminalStatus, DateTimeOffset.UtcNow.AddDays(-3));
        await suppliersDb.SaveChangesAsync(CancellationToken.None);

        await handler.HandleAsync(null, new FakeBackgroundJobContext(), CancellationToken.None);

        Assert.False(await commerceDb.OperationalAlerts.AnyAsync(a => a.Code == "STUCK_FULFILLMENT"));
    }

    [Fact]
    public async Task Handle_UnknownStatus_RecentlyReconciled_IsNotStuck_EvenThoughCreatedLongAgo()
    {
        // A normal retry interval that's still within its expected window: the sweep has been
        // reconciling this every 2 minutes for the last 20 minutes without resolving it yet — that's
        // active reconciliation, not "idle", so it must not be flagged.
        var (handler, commerceDb, suppliersDb) = CreateHandler();
        var (supplier, mapping) = SeedSupplier(suppliersDb);
        AddFulfillment(
            suppliersDb, supplier.Id, mapping.Id, SupplierFulfillmentStatus.Unknown,
            createdOnUtc: DateTimeOffset.UtcNow.AddMinutes(-20),
            lastReconciledOnUtc: DateTimeOffset.UtcNow.AddMinutes(-1));
        await suppliersDb.SaveChangesAsync(CancellationToken.None);

        await handler.HandleAsync(null, new FakeBackgroundJobContext(), CancellationToken.None);

        Assert.False(await commerceDb.OperationalAlerts.AnyAsync(a => a.Code == "STUCK_FULFILLMENT"));
    }

    [Fact]
    public async Task Handle_UnknownStatus_LastReconciledOverTenMinutesAgo_IsStuck()
    {
        var (handler, commerceDb, suppliersDb) = CreateHandler();
        var (supplier, mapping) = SeedSupplier(suppliersDb);
        AddFulfillment(
            suppliersDb, supplier.Id, mapping.Id, SupplierFulfillmentStatus.Unknown,
            createdOnUtc: DateTimeOffset.UtcNow.AddHours(-2),
            lastReconciledOnUtc: DateTimeOffset.UtcNow.AddMinutes(-15));
        await suppliersDb.SaveChangesAsync(CancellationToken.None);

        await handler.HandleAsync(null, new FakeBackgroundJobContext(), CancellationToken.None);

        Assert.True(await commerceDb.OperationalAlerts.AnyAsync(a => a.Code == "STUCK_FULFILLMENT"));
    }

    [Fact]
    public async Task Handle_StuckFulfillmentAlert_ContainsInvestigationContext_ButNeverSupplierSecrets()
    {
        var (handler, commerceDb, suppliersDb) = CreateHandler();
        var (supplier, mapping) = SeedSupplier(suppliersDb, apiKey: "top-secret-api-key-value");
        var orderId = Guid.NewGuid();
        var fulfillment = AddFulfillment(
            suppliersDb, supplier.Id, mapping.Id, SupplierFulfillmentStatus.Submitted,
            DateTimeOffset.UtcNow.AddMinutes(-30), orderId: orderId, attempts: 2);
        await suppliersDb.SaveChangesAsync(CancellationToken.None);

        var order = HAMBOX.Modules.Commerce.Domain.Orders.Order.Create(
            "guest", "HB-100001", "customer@example.com", "US", "dev", 10m, 0m, 0m, 10m, []);
        commerceDb.Entry(order).Property("Id").CurrentValue = orderId;
        commerceDb.Orders.Add(order);
        await commerceDb.SaveChangesAsync(CancellationToken.None);

        await handler.HandleAsync(null, new FakeBackgroundJobContext(), CancellationToken.None);

        var alert = await commerceDb.OperationalAlerts.AsNoTracking().SingleAsync(a => a.Code == "STUCK_FULFILLMENT");
        Assert.Equal("SupplierFulfillment", alert.RelatedEntityType);
        Assert.Equal(fulfillment.Id.ToString(), alert.RelatedEntityId);
        Assert.Contains("HB-100001", alert.Message);
        Assert.Contains(supplier.Name, alert.Message);
        Assert.Contains("Test Product", alert.Message);
        Assert.Contains("Submitted", alert.Message);
        Assert.Contains("Attempts: 2", alert.Message);
        Assert.Contains(fulfillment.HamboxReferenceId.ToString(), alert.Message);

        Assert.DoesNotContain("top-secret-api-key-value", alert.Message);
        Assert.DoesNotContain("secret-api-secret-value", alert.Message);
        Assert.DoesNotContain(alert.Title, "top-secret-api-key-value");
    }

    [Fact]
    public async Task Handle_RepeatedSweepsOnTheSameStuckFulfillment_DoesNotDuplicateItsAlert()
    {
        var (handler, commerceDb, suppliersDb) = CreateHandler();
        var (supplier, mapping) = SeedSupplier(suppliersDb);
        AddFulfillment(suppliersDb, supplier.Id, mapping.Id, SupplierFulfillmentStatus.Submitted, DateTimeOffset.UtcNow.AddMinutes(-30));
        await suppliersDb.SaveChangesAsync(CancellationToken.None);

        await handler.HandleAsync(null, new FakeBackgroundJobContext(), CancellationToken.None);
        // Simulates the next 5-minute recurring pass while the fulfillment is still stuck.
        await handler.HandleAsync(null, new FakeBackgroundJobContext(), CancellationToken.None);

        Assert.Equal(1, await commerceDb.OperationalAlerts.CountAsync(a => a.Code == "STUCK_FULFILLMENT"));
    }

    [Fact]
    public async Task Handle_TwoIndependentStuckFulfillments_EachGetsItsOwnIndependentAlert()
    {
        // Also stands in for "a recovered incident can produce a new one": since a terminal fulfillment
        // can never become non-terminal again in this domain, a genuinely new incident is always a
        // different SupplierFulfillment row with its own id — exactly what this proves gets its own,
        // independently deduplicated alert rather than colliding with an older one.
        var (handler, commerceDb, suppliersDb) = CreateHandler();
        var (supplier, mapping) = SeedSupplier(suppliersDb);
        var first = AddFulfillment(suppliersDb, supplier.Id, mapping.Id, SupplierFulfillmentStatus.Submitted, DateTimeOffset.UtcNow.AddMinutes(-15));
        var second = AddFulfillment(suppliersDb, supplier.Id, mapping.Id, SupplierFulfillmentStatus.Unknown, DateTimeOffset.UtcNow.AddMinutes(-45));
        await suppliersDb.SaveChangesAsync(CancellationToken.None);

        await handler.HandleAsync(null, new FakeBackgroundJobContext(), CancellationToken.None);

        var alerts = await commerceDb.OperationalAlerts.AsNoTracking()
            .Where(a => a.Code == "STUCK_FULFILLMENT")
            .ToListAsync(CancellationToken.None);
        Assert.Equal(2, alerts.Count);
        Assert.Contains(alerts, a => a.RelatedEntityId == first.Id.ToString());
        Assert.Contains(alerts, a => a.RelatedEntityId == second.Id.ToString());
    }

    [Fact]
    public async Task Handle_NoActiveFulfillments_DoesNotQueryOrdersOrRaiseAnyStuckFulfillmentAlert()
    {
        var (handler, commerceDb, suppliersDb) = CreateHandler();
        var (supplier, mapping) = SeedSupplier(suppliersDb);
        AddFulfillment(suppliersDb, supplier.Id, mapping.Id, SupplierFulfillmentStatus.Succeeded, DateTimeOffset.UtcNow.AddDays(-1));
        await suppliersDb.SaveChangesAsync(CancellationToken.None);

        await handler.HandleAsync(null, new FakeBackgroundJobContext(), CancellationToken.None);

        Assert.False(await commerceDb.OperationalAlerts.AnyAsync(a => a.Code == "STUCK_FULFILLMENT"));
    }
}
