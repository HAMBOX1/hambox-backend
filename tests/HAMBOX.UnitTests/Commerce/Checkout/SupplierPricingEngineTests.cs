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
/// Covers <see cref="SupplierPricingEngine"/> — the margin-aware re-ranking layered on top of
/// <see cref="SupplierRoutingEngine"/> (whose own eligibility filtering and cost ranking is covered by
/// <see cref="SupplierRoutingEngineTests"/> and is not re-tested here). The one thing this layer adds
/// that the routing engine cannot answer: when suppliers carry different margins, the cheapest-COST
/// supplier is not always the cheapest-to-CUSTOMER supplier.
/// </summary>
public sealed class SupplierPricingEngineTests
{
    private static (SupplierPricingEngine Engine, HAMBOX.Modules.Suppliers.Infrastructure.Persistence.SuppliersDbContext Db) CreateEngine(
        decimal defaultMarginPercent, params string[] providerTypes)
    {
        var db = SuppliersTestDbContextFactory.Create();
        var providers = providerTypes.Select(t => new FakeSupplierProvider(t)).ToArray();
        var registry = new SupplierProviderRegistry(providers);

        var exchangeRateService = new CurrencyExchangeRateService(
            new FakeCurrencyExchangeRateProvider(),
            new MemoryCache(new MemoryCacheOptions()),
            Options.Create(new CurrencySettings()),
            TimeProvider.System,
            new ServiceCollection().BuildServiceProvider().GetRequiredService<IServiceScopeFactory>());

        var routingEngine = new SupplierRoutingEngine(db, registry, exchangeRateService, Options.Create(new SupplierAvailabilityOptions()));
        var settingsProvider = new FakeCommerceSettingsProvider
        {
            Commerce = new(0m, false, 15, 24, 14, "INV-", DefaultSupplierMarginPercent: defaultMarginPercent),
        };
        var engine = new SupplierPricingEngine(routingEngine, settingsProvider);
        return (engine, db);
    }

    private static async Task<(Supplier Supplier, SupplierProductMapping Mapping)> SeedSupplierAsync(
        HAMBOX.Modules.Suppliers.Infrastructure.Persistence.SuppliersDbContext db,
        string providerType,
        Guid productId,
        decimal buyingPrice,
        decimal? marginPercentOverride = null,
        string currency = "USD",
        int priority = 0,
        string externalProductId = "EXT-1")
    {
        var supplier = Supplier.Create(providerType, $"SUP-{Guid.NewGuid():N}", providerType, SupplierAuthenticationType.None, null, priority);
        db.Suppliers.Add(supplier);

        var mapping = SupplierProductMapping.Create(
            supplier.Id, productId, externalProductId, null, null, buyingPrice, currency, priority,
            internalProductVariantId: null, marginPercentOverride: marginPercentOverride);
        db.SupplierProductMappings.Add(mapping);
        await db.SaveChangesAsync();

        var availability = SupplierProductAvailability.CreateUnknown(supplier.Id, mapping.Id, externalProductId);
        availability.RecordChecked(SupplierAvailabilityState.Available, null, DateTimeOffset.UtcNow, externalProductId);
        db.SupplierProductAvailabilities.Add(availability);
        await db.SaveChangesAsync();

        return (supplier, mapping);
    }

    // 1. The user's own worked example: A=$7.45 cost/20% margin=$8.94 selling; B=$6.90/20%=$8.28 selling.
    // B has both the lower cost AND the lower selling price here, so this also proves the "normal" case
    // (uniform margin) still picks the same winner cost-ranking would.
    [Fact]
    public async Task ResolveAsync_UniformMargin_CheapestCostAlsoCheapestSellingPrice()
    {
        var (engine, db) = CreateEngine(defaultMarginPercent: 20m, "Bamboo", "Visoria");
        var productId = Guid.NewGuid();
        await SeedSupplierAsync(db, "Bamboo", productId, 7.45m);
        var (supplierB, mappingB) = await SeedSupplierAsync(db, "Visoria", productId, 6.90m);

        var result = await engine.ResolveAsync(new SupplierRoutingRequest(productId, Guid.NewGuid(), 1));

        Assert.Equal(2, result.RankedBySellingPriceAscending.Count);
        var winner = result.RankedBySellingPriceAscending[0];
        Assert.Equal(supplierB.Id, winner.SupplierId);
        Assert.Equal(mappingB.Id, winner.SupplierProductMappingId);
        Assert.Equal(8.28m, winner.SellingPrice);
        Assert.Equal(20m, winner.MarginPercentApplied);
    }

    // Margin inversion: higher cost + lower margin beats lower cost + higher margin on SELLING price,
    // even though a cost-only ranking (SupplierRoutingEngine) would pick the other supplier first.
    [Fact]
    public async Task ResolveAsync_DifferentMargins_SellingPriceWinnerDiffersFromCostWinner()
    {
        var (engine, db) = CreateEngine(defaultMarginPercent: 20m, "Bamboo", "Visoria");
        var productId = Guid.NewGuid();
        // Cheaper cost, but a much higher per-mapping margin override -> higher selling price ($9*1.50=$13.50).
        var (cheapCostSupplier, _) = await SeedSupplierAsync(db, "Bamboo", productId, 9.00m, marginPercentOverride: 50m);
        // More expensive cost, but the low platform-default margin -> lower selling price ($10*1.20=$12.00).
        var (cheapPriceSupplier, cheapPriceMapping) = await SeedSupplierAsync(db, "Visoria", productId, 10.00m);

        var result = await engine.ResolveAsync(new SupplierRoutingRequest(productId, Guid.NewGuid(), 1));

        var winner = result.RankedBySellingPriceAscending[0];
        Assert.Equal(cheapPriceSupplier.Id, winner.SupplierId);
        Assert.Equal(cheapPriceMapping.Id, winner.SupplierProductMappingId);
        Assert.Equal(12.00m, winner.SellingPrice);
        Assert.NotEqual(cheapCostSupplier.Id, winner.SupplierId);
    }

    // Per-mapping margin override takes precedence over the platform default.
    [Fact]
    public async Task ResolveAsync_MarginOverride_TakesPrecedenceOverPlatformDefault()
    {
        var (engine, db) = CreateEngine(defaultMarginPercent: 20m, "Bamboo");
        var productId = Guid.NewGuid();
        await SeedSupplierAsync(db, "Bamboo", productId, 10.00m, marginPercentOverride: 5m);

        var result = await engine.ResolveAsync(new SupplierRoutingRequest(productId, Guid.NewGuid(), 1));

        var winner = Assert.Single(result.RankedBySellingPriceAscending);
        Assert.Equal(5m, winner.MarginPercentApplied);
        Assert.Equal(10.50m, winner.SellingPrice);
    }

    // No eligible supplier at all -> empty ranked list, safe for the caller to fall back to catalog pricing.
    [Fact]
    public async Task ResolveAsync_NoEligibleSupplier_ReturnsEmptyRankedList()
    {
        var (engine, _) = CreateEngine(defaultMarginPercent: 20m);

        var result = await engine.ResolveAsync(new SupplierRoutingRequest(Guid.NewGuid(), Guid.NewGuid(), 1));

        Assert.Empty(result.RankedBySellingPriceAscending);
    }

    // Equal selling prices -> deterministic tie-break (Priority ascending, then SupplierId), same rule
    // SupplierRoutingEngine uses, applied one layer up.
    [Fact]
    public async Task ResolveAsync_EqualSellingPrice_TieBreaksByPriorityThenSupplierId_Deterministically()
    {
        var (engine, db) = CreateEngine(defaultMarginPercent: 10m, "Bamboo", "Visoria");
        var productId = Guid.NewGuid();
        await SeedSupplierAsync(db, "Bamboo", productId, 10m, priority: 5);
        var (visoria, _) = await SeedSupplierAsync(db, "Visoria", productId, 10m, priority: 1);

        var first = await engine.ResolveAsync(new SupplierRoutingRequest(productId, Guid.NewGuid(), 1));
        var second = await engine.ResolveAsync(new SupplierRoutingRequest(productId, Guid.NewGuid(), 1));

        Assert.Equal(visoria.Id, first.RankedBySellingPriceAscending[0].SupplierId);
        Assert.Equal(first.RankedBySellingPriceAscending[0].SupplierId, second.RankedBySellingPriceAscending[0].SupplierId);
    }

    // Currency conversion happens BEFORE margin is applied — a EUR-priced mapping is normalized to the
    // base currency first, then margin is applied to the normalized (not the original) amount.
    [Fact]
    public async Task ResolveAsync_NonBaseCurrency_ConvertsBeforeApplyingMargin()
    {
        var (engine, db) = CreateEngine(defaultMarginPercent: 20m, "Bamboo");
        var productId = Guid.NewGuid();
        // 9.20 EUR / 0.92 (EUR per USD) = 10.00 USD normalized cost -> *1.20 margin = 12.00 selling.
        await SeedSupplierAsync(db, "Bamboo", productId, 9.20m, currency: "EUR");

        var result = await engine.ResolveAsync(new SupplierRoutingRequest(productId, Guid.NewGuid(), 1));

        var winner = Assert.Single(result.RankedBySellingPriceAscending);
        Assert.Equal(10.00m, winner.CostInBaseCurrency);
        Assert.Equal(12.00m, winner.SellingPrice);
        Assert.Equal("EUR", winner.OriginalCurrency);
        Assert.Equal(9.20m, winner.OriginalCost);
    }
}
