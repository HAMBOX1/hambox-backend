using HAMBOX.Modules.Suppliers.Application.Abstractions;
using HAMBOX.Modules.Suppliers.Application.Services;
using HAMBOX.Modules.Suppliers.Domain.Fulfillments;
using HAMBOX.Modules.Suppliers.Domain.Suppliers;
using HAMBOX.Modules.Suppliers.Infrastructure.Persistence;
using HAMBOX.Modules.Suppliers.Infrastructure.Services;
using HAMBOX.UnitTests.Suppliers.TestDoubles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace HAMBOX.UnitTests.Suppliers;

public sealed class SupplierFulfillmentServiceTests
{
    private static (SuppliersDbContext Db, SupplierFulfillmentService Service, FakeSupplierProvider Provider) CreateHarness(ILogger<SupplierFulfillmentService>? logger = null)
    {
        var db = SuppliersTestDbContextFactory.Create();
        var provider = new FakeSupplierProvider("Fake");
        var registry = new SupplierProviderRegistry([provider]);
        var service = new SupplierFulfillmentService(db, registry, logger ?? NullLogger<SupplierFulfillmentService>.Instance);
        return (db, service, provider);
    }

    private static async Task<(Supplier Supplier, SupplierProductMapping Mapping)> SeedSupplierAsync(
        SuppliersDbContext db, bool enabled = true, string providerType = "Fake")
    {
        var supplier = Supplier.Create("Test Supplier", $"SUP-{Guid.NewGuid():N}", providerType, SupplierAuthenticationType.None, null, 0);
        if (!enabled)
        {
            supplier.Disable();
        }

        db.Suppliers.Add(supplier);

        var mapping = SupplierProductMapping.Create(supplier.Id, Guid.NewGuid(), "EXT-1", null, null, 5m, "USD", 0);
        db.SupplierProductMappings.Add(mapping);

        await db.SaveChangesAsync();
        return (supplier, mapping);
    }

    // A. Successful purchase
    [Fact]
    public async Task ProcessAsync_SuccessfulPurchase_MarksSucceeded_AndReturnsDeliveredCodes()
    {
        var (db, service, provider) = CreateHarness();
        var (supplier, mapping) = await SeedSupplierAsync(db);

        var created = await service.RequestFulfillmentAsync(new SupplierFulfillmentRequest(Guid.NewGuid(), Guid.NewGuid(), supplier.Id, mapping.Id, 3));
        var result = await service.ProcessAsync(created.Value.FulfillmentId);

        Assert.True(result.IsSuccess);
        Assert.Equal(SupplierFulfillmentStatus.Succeeded, result.Value.Status);
        Assert.Equal(3, result.Value.DeliveredQuantity);
        Assert.Equal(0, result.Value.RemainingQuantity);
        Assert.NotNull(result.Value.DeliveredCodes);
        Assert.Equal(3, result.Value.DeliveredCodes!.Count);
        Assert.Single(provider.PurchaseCalls);
        Assert.Equal(created.Value.HamboxReferenceId.ToString(), provider.PurchaseCalls[0].ReferenceId);
    }

    // B. Explicit provider failure
    [Fact]
    public async Task ProcessAsync_ExplicitProviderFailure_MarksFailed_WithMappedCategory()
    {
        var (db, service, provider) = CreateHarness();
        var (supplier, mapping) = await SeedSupplierAsync(db);
        provider.PurchaseResponder = (_, _) =>
            new SupplierPurchaseResult(false, null, null, SupplierFulfillmentFailureCategory.InsufficientSupplierBalance, "no balance");

        var created = await service.RequestFulfillmentAsync(new SupplierFulfillmentRequest(Guid.NewGuid(), Guid.NewGuid(), supplier.Id, mapping.Id, 2));
        var result = await service.ProcessAsync(created.Value.FulfillmentId);

        Assert.True(result.IsSuccess);
        Assert.Equal(SupplierFulfillmentStatus.Failed, result.Value.Status);
        Assert.Equal(SupplierFulfillmentFailureCategory.InsufficientSupplierBalance, result.Value.FailureCategory);
        Assert.Equal(0, result.Value.DeliveredQuantity);
    }

    // C. Provider timeout -> Unknown
    [Fact]
    public async Task ProcessAsync_ProviderThrows_MarksUnknown_NeverFailed()
    {
        var (db, service, provider) = CreateHarness();
        var (supplier, mapping) = await SeedSupplierAsync(db);
        provider.PurchaseThrows = (_, _) => new TimeoutException("simulated timeout");

        var created = await service.RequestFulfillmentAsync(new SupplierFulfillmentRequest(Guid.NewGuid(), Guid.NewGuid(), supplier.Id, mapping.Id, 2));
        var result = await service.ProcessAsync(created.Value.FulfillmentId);

        Assert.True(result.IsSuccess);
        Assert.Equal(SupplierFulfillmentStatus.Unknown, result.Value.Status);
    }

    // D. Unknown -> reconciliation success
    [Fact]
    public async Task ReconcileAsync_FromUnknown_ResolvesToSucceeded_WithoutRepurchasing()
    {
        var (db, service, provider) = CreateHarness();
        var (supplier, mapping) = await SeedSupplierAsync(db);
        provider.PurchaseThrows = (_, _) => new TimeoutException("simulated timeout");

        var created = await service.RequestFulfillmentAsync(new SupplierFulfillmentRequest(Guid.NewGuid(), Guid.NewGuid(), supplier.Id, mapping.Id, 2));
        await service.ProcessAsync(created.Value.FulfillmentId);

        provider.StatusResponder = (_, _) => new SupplierOrderStatusResult(SupplierProviderOrderStatus.Succeeded, "PROV-1", ["C1", "C2"], null, null);

        var result = await service.ReconcileAsync(created.Value.FulfillmentId);

        Assert.True(result.IsSuccess);
        Assert.Equal(SupplierFulfillmentStatus.Succeeded, result.Value.Status);
        Assert.Equal(2, result.Value.DeliveredQuantity);
        Assert.Single(provider.PurchaseCalls); // reconciliation never re-purchases
        Assert.Single(provider.StatusCalls);
    }

    // E. Unknown -> reconciliation failure
    [Fact]
    public async Task ReconcileAsync_FromUnknown_ResolvesToFailed_WithoutRepurchasing()
    {
        var (db, service, provider) = CreateHarness();
        var (supplier, mapping) = await SeedSupplierAsync(db);
        provider.PurchaseThrows = (_, _) => new TimeoutException("simulated timeout");

        var created = await service.RequestFulfillmentAsync(new SupplierFulfillmentRequest(Guid.NewGuid(), Guid.NewGuid(), supplier.Id, mapping.Id, 2));
        await service.ProcessAsync(created.Value.FulfillmentId);

        provider.StatusResponder = (_, _) =>
            new SupplierOrderStatusResult(SupplierProviderOrderStatus.Failed, null, null, SupplierFulfillmentFailureCategory.ProductUnavailable, "out of stock");

        var result = await service.ReconcileAsync(created.Value.FulfillmentId);

        Assert.True(result.IsSuccess);
        Assert.Equal(SupplierFulfillmentStatus.Failed, result.Value.Status);
        Assert.Equal(SupplierFulfillmentFailureCategory.ProductUnavailable, result.Value.FailureCategory);
        Assert.Single(provider.PurchaseCalls);
    }

    // F. Partial fulfillment
    [Fact]
    public async Task ProcessAsync_PartialDelivery_MarksPartialFailed_WithRemainingQuantity()
    {
        var (db, service, provider) = CreateHarness();
        var (supplier, mapping) = await SeedSupplierAsync(db);
        provider.PurchaseResponder = (_, _) => new SupplierPurchaseResult(true, "PROV-1", ["C1", "C2"], null, null);

        var created = await service.RequestFulfillmentAsync(new SupplierFulfillmentRequest(Guid.NewGuid(), Guid.NewGuid(), supplier.Id, mapping.Id, 5));
        var result = await service.ProcessAsync(created.Value.FulfillmentId);

        Assert.True(result.IsSuccess);
        Assert.Equal(SupplierFulfillmentStatus.PartialFailed, result.Value.Status);
        Assert.Equal(2, result.Value.DeliveredQuantity);
        Assert.Equal(3, result.Value.RemainingQuantity);
    }

    // G. Partial fulfillment -> retry remaining quantity, via the sweep
    [Fact]
    public async Task ProcessDueFulfillmentsAsync_CreatesFollowUp_ForPartialFailedRemainder_WithNewReference()
    {
        var (db, service, provider) = CreateHarness();
        var (supplier, mapping) = await SeedSupplierAsync(db);
        var orderId = Guid.NewGuid();
        var orderItemId = Guid.NewGuid();

        provider.PurchaseResponder = (_, _) => new SupplierPurchaseResult(true, "PROV-1", ["C1", "C2"], null, null);
        var created = await service.RequestFulfillmentAsync(new SupplierFulfillmentRequest(orderId, orderItemId, supplier.Id, mapping.Id, 5));
        await service.ProcessAsync(created.Value.FulfillmentId); // -> PartialFailed, remaining 3

        var sweep = await service.ProcessDueFulfillmentsAsync(50);

        Assert.Equal(1, sweep.FollowUpsCreated);

        var all = await db.SupplierFulfillments.Where(f => f.OrderItemId == orderItemId).ToListAsync();
        Assert.Equal(2, all.Count);
        var followUp = Assert.Single(all, f => f.Id != created.Value.FulfillmentId);
        Assert.Equal(3, followUp.RequestedQuantity);
        Assert.Equal(SupplierFulfillmentStatus.Pending, followUp.Status);
        Assert.NotEqual(created.Value.HamboxReferenceId, followUp.HamboxReferenceId);
    }

    // Hot-loop guard for repeated PartialFailed follow-ups
    [Fact]
    public async Task ProcessDueFulfillmentsAsync_StopsCreatingFollowUps_PastTheAttemptCap()
    {
        var (db, service, provider) = CreateHarness();
        var (supplier, mapping) = await SeedSupplierAsync(db);
        var orderId = Guid.NewGuid();
        var orderItemId = Guid.NewGuid();

        provider.PurchaseResponder = (req, _) => new SupplierPurchaseResult(true, $"PROV-{req.ReferenceId}", ["C1"], null, null); // always delivers exactly 1

        var created = await service.RequestFulfillmentAsync(new SupplierFulfillmentRequest(orderId, orderItemId, supplier.Id, mapping.Id, 3));
        await service.ProcessAsync(created.Value.FulfillmentId); // 1 of 3 -> PartialFailed

        // Drive the sweep repeatedly; each pass claims/processes the newly created follow-up and, being
        // partial again, would create another — until the attempt cap stops it.
        for (var i = 0; i < 5; i++)
        {
            var due = await db.SupplierFulfillments.Where(f => f.OrderItemId == orderItemId && f.Status == SupplierFulfillmentStatus.Pending).ToListAsync();
            foreach (var f in due)
            {
                await service.ProcessAsync(f.Id);
            }

            await service.ProcessDueFulfillmentsAsync(50);
        }

        var totalAttempts = await db.SupplierFulfillments.CountAsync(f => f.OrderItemId == orderItemId);
        Assert.True(totalAttempts <= 3, $"expected the attempt cap to stop growth, got {totalAttempts} attempts");
    }

    // H. Duplicate request uses same HamboxReferenceId
    [Fact]
    public async Task RequestFulfillmentAsync_CalledTwice_ForSameOpenScope_ReusesSameFulfillment()
    {
        var (db, service, _) = CreateHarness();
        var (supplier, mapping) = await SeedSupplierAsync(db);
        var request = new SupplierFulfillmentRequest(Guid.NewGuid(), Guid.NewGuid(), supplier.Id, mapping.Id, 3);

        var first = await service.RequestFulfillmentAsync(request);
        var second = await service.RequestFulfillmentAsync(request);

        Assert.Equal(first.Value.FulfillmentId, second.Value.FulfillmentId);
        Assert.Equal(first.Value.HamboxReferenceId, second.Value.HamboxReferenceId);

        var count = await db.SupplierFulfillments.CountAsync(f => f.OrderItemId == request.OrderItemId);
        Assert.Equal(1, count);
    }

    // K. Provider duplicate response is handled safely — the fake's own duplicate-reference detection
    [Fact]
    public async Task FakeSupplierProvider_DetectsDuplicateReferenceId_WhenCalledTwiceWithSameReference()
    {
        var provider = new FakeSupplierProvider("Fake");
        var context = new SupplierProviderContext(Guid.NewGuid(), "CODE", null, new SupplierProviderCredentials(null, null, null, null, null, null), null);
        var request = new SupplierPurchaseRequest("EXT-1", 1, null, "same-reference");

        await provider.PurchaseAsync(request, context);
        await provider.PurchaseAsync(request, context);

        Assert.Equal(1, provider.DuplicateReferenceIdCount);
        Assert.Equal(2, provider.PurchaseCalls.Count);
    }

    // K (continued). Re-processing an already-terminal attempt never calls the provider again
    [Fact]
    public async Task ProcessAsync_CalledAgain_AfterAlreadyTerminal_DoesNotCallProviderAgain()
    {
        var (db, service, provider) = CreateHarness();
        var (supplier, mapping) = await SeedSupplierAsync(db);

        var created = await service.RequestFulfillmentAsync(new SupplierFulfillmentRequest(Guid.NewGuid(), Guid.NewGuid(), supplier.Id, mapping.Id, 1));
        await service.ProcessAsync(created.Value.FulfillmentId);

        var again = await service.ProcessAsync(created.Value.FulfillmentId);

        Assert.True(again.IsSuccess);
        Assert.Equal(SupplierFulfillmentStatus.Succeeded, again.Value.Status);
        Assert.Single(provider.PurchaseCalls);
    }

    // L. Unsupported ProviderType
    [Fact]
    public async Task ProcessAsync_UnsupportedProviderType_FailsSafely_WithoutCallingAnyProvider_AndWithoutClaiming()
    {
        var (db, service, provider) = CreateHarness();
        var supplier = Supplier.Create("Unregistered", $"SUP-{Guid.NewGuid():N}", "NotRegistered", SupplierAuthenticationType.None, null, 0);
        db.Suppliers.Add(supplier);
        var mapping = SupplierProductMapping.Create(supplier.Id, Guid.NewGuid(), "EXT-1", null, null, 1m, "USD", 0);
        db.SupplierProductMappings.Add(mapping);
        await db.SaveChangesAsync();

        var created = await service.RequestFulfillmentAsync(new SupplierFulfillmentRequest(Guid.NewGuid(), Guid.NewGuid(), supplier.Id, mapping.Id, 1));
        var result = await service.ProcessAsync(created.Value.FulfillmentId);

        Assert.True(result.IsFailure);
        Assert.Equal("Supplier.ProviderTypeNotRegistered", result.Error.Code);
        Assert.Empty(provider.PurchaseCalls);

        var reloaded = await db.SupplierFulfillments.AsNoTracking().FirstAsync(f => f.Id == created.Value.FulfillmentId);
        Assert.Equal(SupplierFulfillmentStatus.Pending, reloaded.Status); // never claimed
    }

    // M. Disabled supplier — at request time
    [Fact]
    public async Task RequestFulfillmentAsync_DisabledSupplier_FailsSafely()
    {
        var (db, service, _) = CreateHarness();
        var (supplier, mapping) = await SeedSupplierAsync(db, enabled: false);

        var result = await service.RequestFulfillmentAsync(new SupplierFulfillmentRequest(Guid.NewGuid(), Guid.NewGuid(), supplier.Id, mapping.Id, 1));

        Assert.True(result.IsFailure);
        Assert.Equal("Supplier.SupplierDisabled", result.Error.Code);
    }

    // M (continued). Disabled supplier — discovered at process time, never claims
    [Fact]
    public async Task ProcessAsync_SupplierDisabledAfterCreation_SkipsWithoutClaiming_AndWithoutCallingProvider()
    {
        var (db, service, provider) = CreateHarness();
        var (supplier, mapping) = await SeedSupplierAsync(db);

        var created = await service.RequestFulfillmentAsync(new SupplierFulfillmentRequest(Guid.NewGuid(), Guid.NewGuid(), supplier.Id, mapping.Id, 1));

        var tracked = await db.Suppliers.FirstAsync(s => s.Id == supplier.Id);
        tracked.Disable();
        await db.SaveChangesAsync();

        var result = await service.ProcessAsync(created.Value.FulfillmentId);

        Assert.True(result.IsFailure);
        Assert.Equal("Supplier.SupplierDisabled", result.Error.Code);
        Assert.Empty(provider.PurchaseCalls);

        var reloaded = await db.SupplierFulfillments.AsNoTracking().FirstAsync(f => f.Id == created.Value.FulfillmentId);
        Assert.Equal(SupplierFulfillmentStatus.Pending, reloaded.Status);
    }

    // N. Missing supplier
    [Fact]
    public async Task RequestFulfillmentAsync_MissingSupplier_FailsSafely()
    {
        var (_, service, _) = CreateHarness();

        var result = await service.RequestFulfillmentAsync(new SupplierFulfillmentRequest(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1));

        Assert.True(result.IsFailure);
        Assert.Equal("Supplier.NotFound", result.Error.Code);
    }

    // O. Invalid quantity
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task RequestFulfillmentAsync_InvalidQuantity_FailsSafely(int quantity)
    {
        var (db, service, _) = CreateHarness();
        var (supplier, mapping) = await SeedSupplierAsync(db);

        var result = await service.RequestFulfillmentAsync(new SupplierFulfillmentRequest(Guid.NewGuid(), Guid.NewGuid(), supplier.Id, mapping.Id, quantity));

        Assert.True(result.IsFailure);
        Assert.Equal("Supplier.InvalidFulfillmentQuantity", result.Error.Code);
    }

    // P. Delivered quantity cannot exceed requested quantity — a malformed provider response is
    // rejected (fail closed), never silently trusted, never throws out of the service.
    [Fact]
    public async Task ProcessAsync_ProviderReturnsMoreCodesThanRequested_FailsClosed_WithoutThrowing()
    {
        var (db, service, provider) = CreateHarness();
        var (supplier, mapping) = await SeedSupplierAsync(db);
        provider.PurchaseResponder = (_, _) => new SupplierPurchaseResult(true, "PROV-1", ["C1", "C2", "C3"], null, null);

        var created = await service.RequestFulfillmentAsync(new SupplierFulfillmentRequest(Guid.NewGuid(), Guid.NewGuid(), supplier.Id, mapping.Id, 2));
        var result = await service.ProcessAsync(created.Value.FulfillmentId);

        Assert.True(result.IsSuccess); // the operation itself completed cleanly
        Assert.Equal(SupplierFulfillmentStatus.Failed, result.Value.Status);
        Assert.Equal(SupplierFulfillmentFailureCategory.UnknownProviderState, result.Value.FailureCategory);
    }

    // Supplier A's mapping cannot be used to request fulfillment against Supplier B (IDOR-style guard)
    [Fact]
    public async Task RequestFulfillmentAsync_MappingBelongsToDifferentSupplier_FailsSafely()
    {
        var (db, service, _) = CreateHarness();
        var (supplierA, _) = await SeedSupplierAsync(db);
        var supplierB = Supplier.Create("Supplier B", $"SUP-{Guid.NewGuid():N}", "Fake", SupplierAuthenticationType.None, null, 0);
        db.Suppliers.Add(supplierB);
        var mappingForA = SupplierProductMapping.Create(supplierA.Id, Guid.NewGuid(), "EXT-A", null, null, 1m, "USD", 0);
        db.SupplierProductMappings.Add(mappingForA);
        await db.SaveChangesAsync();

        var result = await service.RequestFulfillmentAsync(new SupplierFulfillmentRequest(Guid.NewGuid(), Guid.NewGuid(), supplierB.Id, mappingForA.Id, 1));

        Assert.True(result.IsFailure);
        Assert.Equal("Supplier.MappingBelongsToAnotherSupplier", result.Error.Code);
    }

    // R. A per-attempt failure during a sweep does not stop the rest of the batch from being processed
    [Fact]
    public async Task ProcessDueFulfillmentsAsync_OneAttemptFailingToProcess_DoesNotStopOthersInTheBatch()
    {
        var (db, service, provider) = CreateHarness();
        var (supplier, mapping) = await SeedSupplierAsync(db);

        var ok = await service.RequestFulfillmentAsync(new SupplierFulfillmentRequest(Guid.NewGuid(), Guid.NewGuid(), supplier.Id, mapping.Id, 1));

        // Simulates an admin deleting a mapping between attempt creation and the sweep running.
        var badFulfillment = SupplierFulfillment.Create(Guid.NewGuid(), Guid.NewGuid(), supplier.Id, Guid.NewGuid(), 1);
        db.SupplierFulfillments.Add(badFulfillment);
        await db.SaveChangesAsync();

        var sweep = await service.ProcessDueFulfillmentsAsync(50);

        Assert.Equal(2, sweep.AttemptsExamined);
        Assert.Equal(1, sweep.Submitted);
        Assert.Equal(1, sweep.Errors);

        var okReloaded = await db.SupplierFulfillments.AsNoTracking().FirstAsync(f => f.Id == ok.Value.FulfillmentId);
        Assert.Equal(SupplierFulfillmentStatus.Succeeded, okReloaded.Status);
    }

    // S. Reconciliation never triggers a second purchase — asserted directly above (D/E) via
    // Assert.Single(provider.PurchaseCalls); duplicated here against a Submitted (not just Unknown) row.
    [Fact]
    public async Task ReconcileAsync_FromSubmitted_NeverCallsPurchaseAgain()
    {
        var (db, service, provider) = CreateHarness();
        var (supplier, mapping) = await SeedSupplierAsync(db);
        provider.PurchaseResponder = (_, _) => new SupplierPurchaseResult(true, "PROV-1", null, null, null); // accepted, outcome unknown yet

        var created = await service.RequestFulfillmentAsync(new SupplierFulfillmentRequest(Guid.NewGuid(), Guid.NewGuid(), supplier.Id, mapping.Id, 2));
        var afterProcess = await service.ProcessAsync(created.Value.FulfillmentId);
        Assert.Equal(SupplierFulfillmentStatus.Submitted, afterProcess.Value.Status);

        provider.StatusResponder = (_, _) => new SupplierOrderStatusResult(SupplierProviderOrderStatus.Succeeded, "PROV-1", ["C1", "C2"], null, null);
        var reconciled = await service.ReconcileAsync(created.Value.FulfillmentId);

        Assert.Equal(SupplierFulfillmentStatus.Succeeded, reconciled.Value.Status);
        Assert.Single(provider.PurchaseCalls);
    }

    // Crash recovery: a row stuck in Submitting (worker died between claim and submit) is recoverable
    // via reconciliation, queried by HamboxReferenceId since no ProviderOrderId was ever recorded.
    [Fact]
    public async Task ReconcileAsync_FromSubmitting_RecoversViaHamboxReferenceId()
    {
        var (db, service, provider) = CreateHarness();
        var (supplier, mapping) = await SeedSupplierAsync(db);

        var created = await service.RequestFulfillmentAsync(new SupplierFulfillmentRequest(Guid.NewGuid(), Guid.NewGuid(), supplier.Id, mapping.Id, 1));
        var tracked = await db.SupplierFulfillments.FirstAsync(f => f.Id == created.Value.FulfillmentId);
        tracked.Claim(); // simulates a worker that claimed and then crashed before calling the provider
        await db.SaveChangesAsync();

        provider.StatusResponder = (query, _) =>
        {
            Assert.Equal(created.Value.HamboxReferenceId, query.HamboxReferenceId);
            Assert.Null(query.ProviderOrderId); // never recorded — must still be resolvable
            return new SupplierOrderStatusResult(SupplierProviderOrderStatus.Succeeded, "PROV-recovered", ["C1"], null, null);
        };

        var result = await service.ReconcileAsync(created.Value.FulfillmentId);

        Assert.True(result.IsSuccess);
        Assert.Equal(SupplierFulfillmentStatus.Succeeded, result.Value.Status);
        Assert.Empty(provider.PurchaseCalls); // never re-purchased
    }

    // T. No secrets ever appear in logged messages, across a success path and an ambiguous-failure path.
    [Fact]
    public async Task Service_NeverLogsCredentials_AcrossSuccessAndAmbiguousFailurePaths()
    {
        var logger = new RecordingLogger<SupplierFulfillmentService>();
        var (db, service, provider) = CreateHarness(logger);

        var supplier = Supplier.Create("Secret Supplier", $"SUP-{Guid.NewGuid():N}", "Fake", SupplierAuthenticationType.ApiKey, null, 0);
        supplier.UpdateCredentials("super-secret-api-key", "super-secret-api-secret", "svc-user", null, "super-secret-bearer-token", null);
        db.Suppliers.Add(supplier);
        var mapping = SupplierProductMapping.Create(supplier.Id, Guid.NewGuid(), "EXT-1", null, null, 1m, "USD", 0);
        db.SupplierProductMappings.Add(mapping);
        await db.SaveChangesAsync();

        var ok = await service.RequestFulfillmentAsync(new SupplierFulfillmentRequest(Guid.NewGuid(), Guid.NewGuid(), supplier.Id, mapping.Id, 1));
        await service.ProcessAsync(ok.Value.FulfillmentId);

        provider.PurchaseThrows = (_, _) => new InvalidOperationException("simulated ambiguous network failure");
        var ambiguous = await service.RequestFulfillmentAsync(new SupplierFulfillmentRequest(Guid.NewGuid(), Guid.NewGuid(), supplier.Id, mapping.Id, 1));
        await service.ProcessAsync(ambiguous.Value.FulfillmentId);

        Assert.NotEmpty(logger.Messages);
        foreach (var message in logger.Messages)
        {
            Assert.DoesNotContain("super-secret-api-key", message, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("super-secret-api-secret", message, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("super-secret-bearer-token", message, StringComparison.OrdinalIgnoreCase);
        }
    }

    // ===================== Concurrency (routing-engine failover support) =====================

    /// <summary>
    /// Two "workers" (separate <see cref="SupplierFulfillmentService"/>/<see cref="SuppliersDbContext"/>
    /// instances, as two API instances would be) racing <see cref="SupplierFulfillmentService.RequestFulfillmentAsync"/>
    /// for the exact same (order, order item, supplier, mapping) scope at once — the scenario a
    /// cheapest-supplier routing engine computing the same ranking on two instances simultaneously could
    /// trigger. Proves <c>IX_SupplierFulfillments_Scope_NonTerminal</c> (the new filtered unique index)
    /// plus <see cref="SupplierFulfillmentService.RequestFulfillmentAsync"/>'s catch-and-recover logic
    /// together close the gap the <see cref="SupplierFulfillment.RowVersion"/> claim alone does not: only
    /// ONE row is ever created, both callers converge on it, and neither throws.
    /// </summary>
    [Fact]
    public async Task RequestFulfillmentAsync_TwoConcurrentCallersForTheSameScope_CreateExactlyOneRow()
    {
        var databaseName = $"concurrency-{Guid.NewGuid():N}";
        var dbForSeeding = SuppliersTestDbContextFactory.Create(databaseName);
        var (supplier, mapping) = await SeedSupplierAsync(dbForSeeding);
        var orderId = Guid.NewGuid();
        var orderItemId = Guid.NewGuid();

        // Each "worker" gets its own DbContext instance against the SAME underlying database — exactly
        // how two API instances would each hold their own scoped context.
        var serviceA = new SupplierFulfillmentService(
            SuppliersTestDbContextFactory.Create(databaseName), new SupplierProviderRegistry([new FakeSupplierProvider("Fake")]), NullLogger<SupplierFulfillmentService>.Instance);
        var serviceB = new SupplierFulfillmentService(
            SuppliersTestDbContextFactory.Create(databaseName), new SupplierProviderRegistry([new FakeSupplierProvider("Fake")]), NullLogger<SupplierFulfillmentService>.Instance);

        var request = new SupplierFulfillmentRequest(orderId, orderItemId, supplier.Id, mapping.Id, 1);

        // Genuinely concurrent — both start their "does this scope already have an open attempt" read
        // before either has committed its insert, which is the actual race the new unique index (and
        // RequestFulfillmentAsync's catch-and-recover around it) exists to close.
        var taskA = serviceA.RequestFulfillmentAsync(request);
        var taskB = serviceB.RequestFulfillmentAsync(request);
        var results = await Task.WhenAll(taskA, taskB);
        var (resultA, resultB) = (results[0], results[1]);

        Assert.True(resultA.IsSuccess);
        Assert.True(resultB.IsSuccess);
        // Both callers converged on the SAME row — never two separate fulfillments for one scope.
        Assert.Equal(resultA.Value.FulfillmentId, resultB.Value.FulfillmentId);

        var verificationDb = SuppliersTestDbContextFactory.Create(databaseName);
        var count = await verificationDb.SupplierFulfillments.CountAsync(f =>
            f.OrderId == orderId && f.OrderItemId == orderItemId && f.SupplierId == supplier.Id && f.SupplierProductMappingId == mapping.Id);
        Assert.Equal(1, count);
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Messages.Add(formatter(state, exception));

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();

            public void Dispose()
            {
            }
        }
    }
}
