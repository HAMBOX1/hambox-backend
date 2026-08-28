using HAMBOX.Infrastructure.Currency;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Infrastructure.Services;
using HAMBOX.Modules.Suppliers.Application.Options;
using HAMBOX.Modules.Suppliers.Domain.Suppliers;
using HAMBOX.Modules.Suppliers.Infrastructure.Services;
using HAMBOX.UnitTests.Commerce.TestDoubles;
using HAMBOX.UnitTests.Suppliers.TestDoubles;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace HAMBOX.UnitTests.Commerce.Checkout;

/// <summary>
/// Covers <see cref="SupplierRoutingEngine"/> — the cheapest-eligible-supplier ranking used by
/// <c>OrderFulfillmentService.QueueAutomatedSupplierFulfillmentAsync</c> for automated purchase
/// selection. Never a live provider call (see <see cref="ISupplierRoutingEngine"/>'s own "fast local
/// decision" contract) — every scenario is driven entirely by seeded <c>Supplier</c>/
/// <c>SupplierProductMapping</c>/<c>SupplierProductAvailability</c> rows and fake providers.
/// </summary>
public sealed class SupplierRoutingEngineTests
{
    private static (SupplierRoutingEngine Engine, HAMBOX.Modules.Suppliers.Infrastructure.Persistence.SuppliersDbContext Db, FakeSupplierProvider[] Providers) CreateEngine(
        params (string ProviderType, int? MaxQuantity)[] providerTypes)
    {
        var db = SuppliersTestDbContextFactory.Create();
        var providers = providerTypes
            .Select(p => new FakeSupplierProvider(p.ProviderType) { MaxQuantityPerPurchase = p.MaxQuantity })
            .ToArray();
        var registry = new SupplierProviderRegistry(providers);

        var exchangeRateService = new CurrencyExchangeRateService(
            new FakeCurrencyExchangeRateProvider(),
            new MemoryCache(new MemoryCacheOptions()),
            Options.Create(new CurrencySettings()),
            TimeProvider.System,
            new ServiceCollection().BuildServiceProvider().GetRequiredService<IServiceScopeFactory>());

        var engine = new SupplierRoutingEngine(db, registry, exchangeRateService, Options.Create(new SupplierAvailabilityOptions()));
        return (engine, db, providers);
    }

    private static async Task<(Supplier Supplier, SupplierProductMapping Mapping)> SeedSupplierAsync(
        HAMBOX.Modules.Suppliers.Infrastructure.Persistence.SuppliersDbContext db,
        string providerType,
        Guid productId,
        decimal buyingPrice,
        string currency = "USD",
        Guid? variantId = null,
        int priority = 0,
        bool isEnabled = true,
        bool available = true,
        string externalProductId = "EXT-1")
    {
        var supplier = Supplier.Create(providerType, $"SUP-{Guid.NewGuid():N}", providerType, SupplierAuthenticationType.None, null, priority);
        if (!isEnabled)
        {
            supplier.Disable();
        }

        db.Suppliers.Add(supplier);

        var mapping = SupplierProductMapping.Create(supplier.Id, productId, externalProductId, null, null, buyingPrice, currency, priority, variantId);
        db.SupplierProductMappings.Add(mapping);
        await db.SaveChangesAsync();

        var availability = SupplierProductAvailability.CreateUnknown(supplier.Id, mapping.Id, externalProductId);
        availability.RecordChecked(
            available ? SupplierAvailabilityState.Available : SupplierAvailabilityState.Unavailable,
            null, DateTimeOffset.UtcNow, externalProductId);
        db.SupplierProductAvailabilities.Add(availability);
        await db.SaveChangesAsync();

        return (supplier, mapping);
    }

    // 1. One mapped supplier -> selected.
    [Fact]
    public async Task ResolveAsync_SingleMappedSupplier_IsSelected()
    {
        var (engine, db, _) = CreateEngine(("Bamboo", null));
        var productId = Guid.NewGuid();
        var (supplier, mapping) = await SeedSupplierAsync(db, "Bamboo", productId, 7.80m);

        var result = await engine.ResolveAsync(new SupplierRoutingRequest(productId, Guid.NewGuid(), 1));

        var candidate = Assert.Single(result.EligibleByCostAscending);
        Assert.Equal(supplier.Id, candidate.SupplierId);
        Assert.Equal(mapping.Id, candidate.SupplierProductMappingId);
        Assert.Equal(7.80m, candidate.CostInBaseCurrency);
    }

    // 2. Two suppliers -> cheapest selected.
    [Fact]
    public async Task ResolveAsync_TwoSuppliers_CheapestSelectedFirst()
    {
        var (engine, db, _) = CreateEngine(("Bamboo", null), ("Visoria", null));
        var productId = Guid.NewGuid();
        await SeedSupplierAsync(db, "Bamboo", productId, 7.80m);
        var (cheapSupplier, _) = await SeedSupplierAsync(db, "Visoria", productId, 7.45m);

        var result = await engine.ResolveAsync(new SupplierRoutingRequest(productId, Guid.NewGuid(), 1));

        Assert.Equal(2, result.EligibleByCostAscending.Count);
        Assert.Equal(cheapSupplier.Id, result.EligibleByCostAscending[0].SupplierId);
        Assert.Equal(7.45m, result.EligibleByCostAscending[0].CostInBaseCurrency);
    }

    // 3. Three suppliers -> cheapest selected (the user's own Xbox Game Pass example: Bamboo $7.80, Visoria $7.45, GlobeTopper $8.10 -> Visoria).
    [Fact]
    public async Task ResolveAsync_ThreeSuppliers_CheapestSelected()
    {
        var (engine, db, _) = CreateEngine(("Bamboo", null), ("Visoria", null), ("GlobeTopper", 1));
        var productId = Guid.NewGuid();
        await SeedSupplierAsync(db, "Bamboo", productId, 7.80m);
        var (visoria, visoriaMapping) = await SeedSupplierAsync(db, "Visoria", productId, 7.45m);
        await SeedSupplierAsync(db, "GlobeTopper", productId, 8.10m);

        var result = await engine.ResolveAsync(new SupplierRoutingRequest(productId, Guid.NewGuid(), 1));

        Assert.Equal(3, result.EligibleByCostAscending.Count);
        Assert.Equal(visoria.Id, result.EligibleByCostAscending[0].SupplierId);
        Assert.Equal(visoriaMapping.Id, result.EligibleByCostAscending[0].SupplierProductMappingId);
    }

    // 4. Cheapest unavailable -> next cheapest selected.
    [Fact]
    public async Task ResolveAsync_CheapestUnavailable_NextCheapestSelected()
    {
        var (engine, db, _) = CreateEngine(("Bamboo", null), ("Visoria", null));
        var productId = Guid.NewGuid();
        var (bamboo, _) = await SeedSupplierAsync(db, "Bamboo", productId, 7.80m);
        await SeedSupplierAsync(db, "Visoria", productId, 7.45m, available: false);

        var result = await engine.ResolveAsync(new SupplierRoutingRequest(productId, Guid.NewGuid(), 1));

        var candidate = Assert.Single(result.EligibleByCostAscending);
        Assert.Equal(bamboo.Id, candidate.SupplierId);
        Assert.Contains(result.Rejected, r => r.Reason.Contains("available", StringComparison.OrdinalIgnoreCase));
    }

    // 5. Cheapest disabled -> next cheapest selected.
    [Fact]
    public async Task ResolveAsync_CheapestDisabled_NextCheapestSelected()
    {
        var (engine, db, _) = CreateEngine(("Bamboo", null), ("Visoria", null));
        var productId = Guid.NewGuid();
        var (bamboo, _) = await SeedSupplierAsync(db, "Bamboo", productId, 7.80m);
        await SeedSupplierAsync(db, "Visoria", productId, 7.45m, isEnabled: false);

        var result = await engine.ResolveAsync(new SupplierRoutingRequest(productId, Guid.NewGuid(), 1));

        var candidate = Assert.Single(result.EligibleByCostAscending);
        Assert.Equal(bamboo.Id, candidate.SupplierId);
        Assert.Contains(result.Rejected, r => r.Reason.Contains("disabled", StringComparison.OrdinalIgnoreCase));
    }

    // 6. Cheapest lacks required capability (quantity) -> next supplier.
    [Fact]
    public async Task ResolveAsync_CheapestLacksQuantityCapability_NextSupplierSelected()
    {
        var (engine, db, _) = CreateEngine(("GlobeTopper", 1), ("Bamboo", null));
        var productId = Guid.NewGuid();
        await SeedSupplierAsync(db, "GlobeTopper", productId, 7.45m); // cheapest, but caps at 1 unit
        var (bamboo, _) = await SeedSupplierAsync(db, "Bamboo", productId, 7.80m);

        var result = await engine.ResolveAsync(new SupplierRoutingRequest(productId, Guid.NewGuid(), Quantity: 3));

        var candidate = Assert.Single(result.EligibleByCostAscending);
        Assert.Equal(bamboo.Id, candidate.SupplierId);
        Assert.Contains(result.Rejected, r => r.Reason.Contains("quantity", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Adds a second mapping to an ALREADY-seeded supplier — a real supplier commonly has one product-wide mapping plus one or more variant-specific overrides.</summary>
    private static async Task<SupplierProductMapping> AddMappingForSameSupplierAsync(
        HAMBOX.Modules.Suppliers.Infrastructure.Persistence.SuppliersDbContext db,
        Guid supplierId, Guid productId, decimal buyingPrice, string externalProductId, Guid? variantId = null)
    {
        var mapping = SupplierProductMapping.Create(supplierId, productId, externalProductId, null, null, buyingPrice, "USD", 0, variantId);
        db.SupplierProductMappings.Add(mapping);
        await db.SaveChangesAsync();

        var availability = SupplierProductAvailability.CreateUnknown(supplierId, mapping.Id, externalProductId);
        availability.RecordChecked(SupplierAvailabilityState.Available, null, DateTimeOffset.UtcNow, externalProductId);
        db.SupplierProductAvailabilities.Add(availability);
        await db.SaveChangesAsync();

        return mapping;
    }

    // 7. Exact variant matching — never accidentally choose a mapping for another variant.
    [Fact]
    public async Task ResolveAsync_VariantSpecificMapping_PreferredOverProductWide_ForMatchingVariant()
    {
        var (engine, db, _) = CreateEngine(("Bamboo", null));
        var productId = Guid.NewGuid();
        var variantId = Guid.NewGuid();
        var otherVariantId = Guid.NewGuid();

        // One supplier, two mappings for the same product: a product-wide default and a cheaper
        // variant-specific override — the real shape this rule exists for.
        var (supplier, _) = await SeedSupplierAsync(db, "Bamboo", productId, 10m, externalProductId: "EXT-WIDE");
        var specificMapping = await AddMappingForSameSupplierAsync(db, supplier.Id, productId, 5m, "EXT-SPECIFIC", variantId);

        var result = await engine.ResolveAsync(new SupplierRoutingRequest(productId, variantId, 1));

        var candidate = Assert.Single(result.EligibleByCostAscending);
        Assert.Equal(specificMapping.Id, candidate.SupplierProductMappingId);
        Assert.Equal(5m, candidate.CostInBaseCurrency);

        // A different variant must never resolve to a mapping scoped to variantId above — this supplier
        // only has that one variant-specific and one product-wide mapping, so the OTHER variant correctly
        // falls back to the product-wide $10 mapping instead.
        var otherResult = await engine.ResolveAsync(new SupplierRoutingRequest(productId, otherVariantId, 1));
        var otherCandidate = Assert.Single(otherResult.EligibleByCostAscending);
        Assert.Equal(10m, otherCandidate.CostInBaseCurrency);
    }

    // 8. Supplier without valid price -> excluded.
    [Fact]
    public async Task ResolveAsync_ZeroBuyingPrice_ExcludedFromComparison()
    {
        var (engine, db, _) = CreateEngine(("Bamboo", null), ("Visoria", null));
        var productId = Guid.NewGuid();
        await SeedSupplierAsync(db, "Bamboo", productId, 0m); // no real price configured
        var (visoria, _) = await SeedSupplierAsync(db, "Visoria", productId, 7.45m);

        var result = await engine.ResolveAsync(new SupplierRoutingRequest(productId, Guid.NewGuid(), 1));

        var candidate = Assert.Single(result.EligibleByCostAscending);
        Assert.Equal(visoria.Id, candidate.SupplierId);
        Assert.Contains(result.Rejected, r => r.Reason.Contains("cost", StringComparison.OrdinalIgnoreCase));
    }

    // 9. Equal prices -> deterministic tie-break (Priority ascending, then SupplierId).
    [Fact]
    public async Task ResolveAsync_EqualCost_TieBreaksByPriorityThenSupplierId_Deterministically()
    {
        var (engine, db, _) = CreateEngine(("Bamboo", null), ("Visoria", null));
        var productId = Guid.NewGuid();
        await SeedSupplierAsync(db, "Bamboo", productId, 5m, priority: 5);
        var (visoria, _) = await SeedSupplierAsync(db, "Visoria", productId, 5m, priority: 1);

        var first = await engine.ResolveAsync(new SupplierRoutingRequest(productId, Guid.NewGuid(), 1));
        var second = await engine.ResolveAsync(new SupplierRoutingRequest(productId, Guid.NewGuid(), 1));

        // Lower Priority wins the tie — never random, and repeated calls never disagree.
        Assert.Equal(visoria.Id, first.EligibleByCostAscending[0].SupplierId);
        Assert.Equal(first.EligibleByCostAscending[0].SupplierId, second.EligibleByCostAscending[0].SupplierId);
    }

    // Manual providers never participate in automated routing, even if otherwise fully "ready".
    [Fact]
    public async Task ResolveAsync_ManualProviderType_NeverEligible()
    {
        var (engine, db, _) = CreateEngine(("Manual", null));
        var productId = Guid.NewGuid();
        await SeedSupplierAsync(db, "Manual", productId, 1m);

        var result = await engine.ResolveAsync(new SupplierRoutingRequest(productId, Guid.NewGuid(), 1));

        Assert.Empty(result.EligibleByCostAscending);
        Assert.Contains(result.Rejected, r => r.Reason.Contains("Manual", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ResolveAsync_NoMappingsAtAll_ReturnsEmptyEligibleAndRejected()
    {
        var (engine, _, _) = CreateEngine();

        var result = await engine.ResolveAsync(new SupplierRoutingRequest(Guid.NewGuid(), Guid.NewGuid(), 1));

        Assert.Empty(result.EligibleByCostAscending);
        Assert.Empty(result.Rejected);
    }
}
