using HAMBOX.Modules.Suppliers.Application.Abstractions;
using HAMBOX.Modules.Suppliers.Application.Services;
using HAMBOX.Modules.Suppliers.Domain.Suppliers;
using HAMBOX.Modules.Suppliers.Infrastructure.Services;
using HAMBOX.UnitTests.Suppliers.TestDoubles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SupplierAvailabilityState = HAMBOX.Modules.Suppliers.Application.Abstractions.SupplierAvailabilityState;
using DomainAvailabilityState = HAMBOX.Modules.Suppliers.Domain.Suppliers.SupplierAvailabilityState;

namespace HAMBOX.UnitTests.Suppliers;

/// <summary>
/// Covers <see cref="SupplierAvailabilityService"/>'s orchestration — sync-cache upsert semantics,
/// safe no-ops, per-supplier failure isolation, and the "never erase last known-good on a failed
/// attempt" persistence rule. <see cref="ISupplierProvider.GetAvailabilityAsync"/> is driven entirely
/// by <see cref="FakeSupplierProvider"/> here — Bamboo's own mapping is covered separately in
/// <c>BambooSupplierProviderTests</c>.
/// </summary>
public sealed class SupplierAvailabilityServiceTests
{
    private static SupplierAvailabilityService CreateService(
        HAMBOX.Modules.Suppliers.Infrastructure.Persistence.SuppliersDbContext db, params ISupplierProvider[] providers) =>
        new(db, new SupplierProviderRegistry(providers), NullLogger<SupplierAvailabilityService>.Instance);

    private static async Task<Supplier> SeedSupplierAsync(
        HAMBOX.Modules.Suppliers.Infrastructure.Persistence.SuppliersDbContext db, bool enabled = true, string providerType = "Fake")
    {
        var supplier = Supplier.Create("Fake Supplier", $"SUP-{Guid.NewGuid():N}", providerType, SupplierAuthenticationType.None, null, 0);
        if (!enabled)
        {
            supplier.Disable();
        }

        db.Suppliers.Add(supplier);
        await db.SaveChangesAsync();
        return supplier;
    }

    private static async Task<SupplierProductMapping> SeedMappingAsync(
        HAMBOX.Modules.Suppliers.Infrastructure.Persistence.SuppliersDbContext db, Guid supplierId, string externalProductId = "EXT-1")
    {
        var mapping = SupplierProductMapping.Create(supplierId, Guid.NewGuid(), externalProductId, null, null, 5m, "USD", 0);
        db.SupplierProductMappings.Add(mapping);
        await db.SaveChangesAsync();
        return mapping;
    }

    [Fact]
    public async Task SyncSupplierAsync_DisabledSupplier_IsSafeNoOp()
    {
        var db = SuppliersTestDbContextFactory.Create();
        var supplier = await SeedSupplierAsync(db, enabled: false);
        await SeedMappingAsync(db, supplier.Id);
        var provider = new FakeSupplierProvider("Fake");
        var service = CreateService(db, provider);

        var result = await service.SyncSupplierAsync(supplier.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.MappingsChecked);
        Assert.Empty(provider.AvailabilityCalls);
    }

    [Fact]
    public async Task SyncSupplierAsync_NoActiveMappings_IsSafeNoOp()
    {
        var db = SuppliersTestDbContextFactory.Create();
        var supplier = await SeedSupplierAsync(db);
        var provider = new FakeSupplierProvider("Fake");
        var service = CreateService(db, provider);

        var result = await service.SyncSupplierAsync(supplier.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.MappingsChecked);
        Assert.Empty(provider.AvailabilityCalls);
    }

    [Fact]
    public async Task SyncSupplierAsync_NoProviderRegistered_IsSafeNoOp()
    {
        var db = SuppliersTestDbContextFactory.Create();
        var supplier = await SeedSupplierAsync(db, providerType: "Unregistered");
        await SeedMappingAsync(db, supplier.Id);
        var service = CreateService(db); // nothing registered

        var result = await service.SyncSupplierAsync(supplier.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.MappingsChecked);
    }

    [Fact]
    public async Task SyncSupplierAsync_NewMapping_NeverSyncedBefore_StartsUnknownUntilResolved()
    {
        var db = SuppliersTestDbContextFactory.Create();
        var supplier = await SeedSupplierAsync(db);
        var mapping = await SeedMappingAsync(db, supplier.Id, "EXT-1");
        var provider = new FakeSupplierProvider("Fake")
        {
            AvailabilityResponse = new SupplierAvailabilityResult(true, [new SupplierAvailabilityItem("EXT-1", SupplierAvailabilityState.Available, 5, DateTimeOffset.UtcNow)], null),
        };
        var service = CreateService(db, provider);

        // Before any sync, no row exists — the storefront/router treats that as Unknown.
        Assert.False(await db.SupplierProductAvailabilities.AnyAsync(a => a.SupplierProductMappingId == mapping.Id));

        var result = await service.SyncSupplierAsync(supplier.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.AvailableCount);
        var row = await db.SupplierProductAvailabilities.SingleAsync(a => a.SupplierProductMappingId == mapping.Id);
        Assert.Equal(DomainAvailabilityState.Available, row.AvailabilityState);
        Assert.Equal(5, row.AvailableQuantity);
        Assert.NotNull(row.LastCheckedAtUtc);
    }

    [Fact]
    public async Task SyncSupplierAsync_ProviderReturnsUnavailable_PersistsUnavailable()
    {
        var db = SuppliersTestDbContextFactory.Create();
        var supplier = await SeedSupplierAsync(db);
        var mapping = await SeedMappingAsync(db, supplier.Id, "EXT-1");
        var provider = new FakeSupplierProvider("Fake")
        {
            AvailabilityResponse = new SupplierAvailabilityResult(true, [new SupplierAvailabilityItem("EXT-1", SupplierAvailabilityState.Unavailable, null, DateTimeOffset.UtcNow)], null),
        };
        var service = CreateService(db, provider);

        var result = await service.SyncSupplierAsync(supplier.Id);

        Assert.Equal(1, result.UnavailableCount);
        var row = await db.SupplierProductAvailabilities.SingleAsync(a => a.SupplierProductMappingId == mapping.Id);
        Assert.Equal(DomainAvailabilityState.Unavailable, row.AvailabilityState);
    }

    [Fact]
    public async Task SyncSupplierAsync_ProviderCallFails_NeverErasesPreviousKnownGoodState()
    {
        var db = SuppliersTestDbContextFactory.Create();
        var supplier = await SeedSupplierAsync(db);
        var mapping = await SeedMappingAsync(db, supplier.Id, "EXT-1");
        var successfulProvider = new FakeSupplierProvider("Fake")
        {
            AvailabilityResponse = new SupplierAvailabilityResult(true, [new SupplierAvailabilityItem("EXT-1", SupplierAvailabilityState.Available, 3, DateTimeOffset.UtcNow)], null),
        };
        await CreateService(db, successfulProvider).SyncSupplierAsync(supplier.Id);
        var firstCheckedAt = (await db.SupplierProductAvailabilities.AsNoTracking().SingleAsync(a => a.SupplierProductMappingId == mapping.Id)).LastCheckedAtUtc;

        // Second sync: the provider call itself now fails outright.
        var failingProvider = new FakeSupplierProvider("Fake") { AvailabilityResponse = new SupplierAvailabilityResult(false, [], "Bamboo unreachable") };
        var result = await CreateService(db, failingProvider).SyncSupplierAsync(supplier.Id);

        Assert.False(result.IsSuccess);
        var row = await db.SupplierProductAvailabilities.AsNoTracking().SingleAsync(a => a.SupplierProductMappingId == mapping.Id);
        // State/quantity/LastCheckedAtUtc from the earlier successful sync must survive untouched.
        Assert.Equal(DomainAvailabilityState.Available, row.AvailabilityState);
        Assert.Equal(3, row.AvailableQuantity);
        Assert.Equal(firstCheckedAt, row.LastCheckedAtUtc);
        Assert.Equal("Bamboo unreachable", row.LastErrorMessage);
    }

    [Fact]
    public async Task SyncSupplierAsync_MultipleMappings_OneProviderCall()
    {
        var db = SuppliersTestDbContextFactory.Create();
        var supplier = await SeedSupplierAsync(db);
        await SeedMappingAsync(db, supplier.Id, "EXT-1");
        await SeedMappingAsync(db, supplier.Id, "EXT-2");
        await SeedMappingAsync(db, supplier.Id, "EXT-3");
        var provider = new FakeSupplierProvider("Fake")
        {
            AvailabilityResponse = new SupplierAvailabilityResult(true,
                [
                    new SupplierAvailabilityItem("EXT-1", SupplierAvailabilityState.Available, null, DateTimeOffset.UtcNow),
                    new SupplierAvailabilityItem("EXT-2", SupplierAvailabilityState.Available, null, DateTimeOffset.UtcNow),
                    new SupplierAvailabilityItem("EXT-3", SupplierAvailabilityState.Unavailable, null, DateTimeOffset.UtcNow),
                ], null),
        };
        var service = CreateService(db, provider);

        var result = await service.SyncSupplierAsync(supplier.Id);

        Assert.Single(provider.AvailabilityCalls); // one call for all three mappings, not three
        Assert.Equal(3, result.MappingsChecked);
        Assert.Equal(2, result.AvailableCount);
        Assert.Equal(1, result.UnavailableCount);
    }

    [Fact]
    public async Task SyncAllEnabledSuppliersAsync_OneSupplierFails_OthersStillSynced()
    {
        var db = SuppliersTestDbContextFactory.Create();
        var failingSupplier = await SeedSupplierAsync(db, providerType: "Failing");
        await SeedMappingAsync(db, failingSupplier.Id, "EXT-1");
        var healthySupplier = await SeedSupplierAsync(db, providerType: "Healthy");
        await SeedMappingAsync(db, healthySupplier.Id, "EXT-2");

        var failingProvider = new FakeSupplierProvider("Failing") { AvailabilityThrows = (_, _) => new InvalidOperationException("boom") };
        var healthyProvider = new FakeSupplierProvider("Healthy")
        {
            AvailabilityResponse = new SupplierAvailabilityResult(true, [new SupplierAvailabilityItem("EXT-2", SupplierAvailabilityState.Available, null, DateTimeOffset.UtcNow)], null),
        };
        var service = CreateService(db, failingProvider, healthyProvider);

        var results = await service.SyncAllEnabledSuppliersAsync();

        Assert.Equal(2, results.Count);
        var failingResult = results.Single(r => r.SupplierId == failingSupplier.Id);
        var healthyResult = results.Single(r => r.SupplierId == healthySupplier.Id);
        Assert.False(failingResult.IsSuccess);
        Assert.True(healthyResult.IsSuccess);
        Assert.Equal(1, healthyResult.AvailableCount); // unaffected by the other supplier's failure
    }
}
