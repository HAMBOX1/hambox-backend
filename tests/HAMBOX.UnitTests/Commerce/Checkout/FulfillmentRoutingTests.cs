using HAMBOX.Application.Fulfillment;
using HAMBOX.Application.Membership;
using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Catalog.Application.Errors;
using HAMBOX.Modules.Catalog.Domain.Categories;
using HAMBOX.Modules.Catalog.Domain.Enums;
using HAMBOX.Modules.Catalog.Domain.Inventory;
using HAMBOX.Modules.Catalog.Domain.Products;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Services;
using HAMBOX.Modules.Commerce.Domain.Account;
using HAMBOX.Modules.Commerce.Domain.Carts;
using HAMBOX.Modules.Commerce.Domain.Orders;
using HAMBOX.Modules.Suppliers.Application.Abstractions;
using HAMBOX.Modules.Suppliers.Application.Options;
using HAMBOX.Modules.Suppliers.Domain.Suppliers;
using HAMBOX.Modules.Suppliers.Infrastructure.Services;
using HAMBOX.SharedKernel.Results;
using HAMBOX.UnitTests.Commerce.TestDoubles;
using HAMBOX.UnitTests.Suppliers.TestDoubles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using DomainAvailabilityState = HAMBOX.Modules.Suppliers.Domain.Suppliers.SupplierAvailabilityState;

namespace HAMBOX.UnitTests.Commerce.Checkout;

/// <summary>
/// Phase 7 — the generic fulfillment-routing system (<see cref="FulfillmentMode"/>,
/// <see cref="IFulfillmentRouter"/>, mode-aware manual/supplier dispatch, and SupplierFirst's
/// terminal-triggered manual fallback). Covers the router's chain resolution, the checkout-time
/// readiness gate, and the end-to-end mode-dispatch behavior — all against the real production
/// classes (<see cref="FulfillmentRouter"/>, <see cref="OrderFulfillmentService"/>,
/// <see cref="CommerceOrderLicenseKeyDeliverySink"/>, the real <c>SupplierFulfillmentService</c>),
/// never a hand-rolled stand-in for the logic actually under test.
/// </summary>
public sealed class FulfillmentRoutingTests
{
    private static async Task<Guid> SeedVariantAsync(
        HAMBOX.Modules.Catalog.Infrastructure.Persistence.CatalogDbContext catalogDb, Guid productId, FulfillmentMode mode)
    {
        var variant = ProductVariant.Create(productId, $"SKU-{Guid.NewGuid():N}");
        variant.Activate();
        variant.SetFulfillmentMode(mode);
        catalogDb.ProductVariants.Add(variant);
        await catalogDb.SaveChangesAsync();
        return variant.Id;
    }

    private static async Task<Supplier> SeedSupplierAsync(
        HAMBOX.Modules.Suppliers.Infrastructure.Persistence.SuppliersDbContext suppliersDb, bool enabled = true)
    {
        var supplier = Supplier.Create("Fake Supplier", $"SUP-{Guid.NewGuid():N}", "Fake", SupplierAuthenticationType.None, null, 0);
        if (!enabled)
        {
            supplier.Disable();
        }

        suppliersDb.Suppliers.Add(supplier);
        await suppliersDb.SaveChangesAsync();
        return supplier;
    }

    /// <summary>
    /// Also seeds a fresh <c>Available</c> <see cref="SupplierProductAvailability"/> row for the new
    /// mapping — since the Supplier Availability phase, READY alone no longer makes
    /// <see cref="FulfillmentRouter"/> return a candidate; every existing test that expects a candidate
    /// to be found relies on this default. Tests specifically exercising the availability dimension
    /// (stale/unavailable/unknown) seed/overwrite the row explicitly instead — see
    /// <see cref="SeedAvailabilityAsync"/>.
    /// </summary>
    private static async Task<SupplierProductMapping> SeedMappingAsync(
        HAMBOX.Modules.Suppliers.Infrastructure.Persistence.SuppliersDbContext suppliersDb,
        Guid supplierId, Guid productId, Guid? variantId = null, int priority = 0)
    {
        var mapping = SupplierProductMapping.Create(supplierId, productId, "EXT-1", null, null, 5m, "USD", priority, variantId);
        suppliersDb.SupplierProductMappings.Add(mapping);
        await suppliersDb.SaveChangesAsync();
        await SeedAvailabilityAsync(suppliersDb, supplierId, mapping.Id, DomainAvailabilityState.Available, DateTimeOffset.UtcNow);
        return mapping;
    }

    private static async Task SeedAvailabilityAsync(
        HAMBOX.Modules.Suppliers.Infrastructure.Persistence.SuppliersDbContext suppliersDb,
        Guid supplierId, Guid mappingId, DomainAvailabilityState state, DateTimeOffset? checkedAtUtc, int? quantity = null)
    {
        var row = SupplierProductAvailability.CreateUnknown(supplierId, mappingId, "EXT-1");
        if (checkedAtUtc is DateTimeOffset checkedAt)
        {
            row.RecordChecked(state, quantity, checkedAt, "EXT-1");
        }

        suppliersDb.SupplierProductAvailabilities.Add(row);
        await suppliersDb.SaveChangesAsync();
    }

    private static FulfillmentRouter CreateRouter(
        HAMBOX.Modules.Catalog.Infrastructure.Persistence.CatalogDbContext catalogDb,
        HAMBOX.Modules.Suppliers.Infrastructure.Persistence.SuppliersDbContext suppliersDb,
        SupplierProviderRegistry registry,
        int staleAfterMinutes = 10) =>
        new(catalogDb, suppliersDb, registry, Options.Create(new SupplierAvailabilityOptions { StaleAfterMinutes = staleAfterMinutes }));

    // ===================== FulfillmentRouter.GetReadinessAsync =====================

    [Fact]
    public async Task GetReadinessAsync_ManualOnly_ReturnsManualAllowedAndNoSupplierCandidate()
    {
        var (_, catalogDb) = CommerceTestDbContextFactory.Create();
        var suppliersDb = SuppliersTestDbContextFactory.Create();
        var registry = new SupplierProviderRegistry([new FakeSupplierProvider("Fake")]);
        var productId = Guid.NewGuid();
        var variantId = await SeedVariantAsync(catalogDb, productId, FulfillmentMode.ManualOnly);
        var supplier = await SeedSupplierAsync(suppliersDb);
        await SeedMappingAsync(suppliersDb, supplier.Id, productId);

        var router = CreateRouter(catalogDb, suppliersDb, registry);
        var readiness = await router.GetReadinessAsync(variantId, CancellationToken.None);

        Assert.Equal(FulfillmentMode.ManualOnly, readiness.Mode);
        Assert.True(readiness.ManualAllowed);
        Assert.Null(readiness.SupplierCandidate);
    }

    [Fact]
    public async Task GetReadinessAsync_ManualFirst_WithReadyMapping_ReturnsCandidate()
    {
        var (_, catalogDb) = CommerceTestDbContextFactory.Create();
        var suppliersDb = SuppliersTestDbContextFactory.Create();
        var registry = new SupplierProviderRegistry([new FakeSupplierProvider("Fake")]);
        var productId = Guid.NewGuid();
        var variantId = await SeedVariantAsync(catalogDb, productId, FulfillmentMode.ManualFirst);
        var supplier = await SeedSupplierAsync(suppliersDb);
        var mapping = await SeedMappingAsync(suppliersDb, supplier.Id, productId);

        var router = CreateRouter(catalogDb, suppliersDb, registry);
        var readiness = await router.GetReadinessAsync(variantId, CancellationToken.None);

        Assert.True(readiness.SupplierReady);
        Assert.Equal(supplier.Id, readiness.SupplierCandidate!.SupplierId);
        Assert.Equal(mapping.Id, readiness.SupplierCandidate.SupplierProductMappingId);
    }

    [Fact]
    public async Task GetReadinessAsync_NoMapping_ReturnsNullCandidate()
    {
        var (_, catalogDb) = CommerceTestDbContextFactory.Create();
        var suppliersDb = SuppliersTestDbContextFactory.Create();
        var registry = new SupplierProviderRegistry([new FakeSupplierProvider("Fake")]);
        var variantId = await SeedVariantAsync(catalogDb, Guid.NewGuid(), FulfillmentMode.SupplierFirst);

        var router = CreateRouter(catalogDb, suppliersDb, registry);
        var readiness = await router.GetReadinessAsync(variantId, CancellationToken.None);

        Assert.False(readiness.SupplierReady);
    }

    [Fact]
    public async Task GetReadinessAsync_DisabledSupplier_IsSkipped()
    {
        var (_, catalogDb) = CommerceTestDbContextFactory.Create();
        var suppliersDb = SuppliersTestDbContextFactory.Create();
        var registry = new SupplierProviderRegistry([new FakeSupplierProvider("Fake")]);
        var productId = Guid.NewGuid();
        var variantId = await SeedVariantAsync(catalogDb, productId, FulfillmentMode.SupplierOnly);
        var supplier = await SeedSupplierAsync(suppliersDb, enabled: false);
        await SeedMappingAsync(suppliersDb, supplier.Id, productId);

        var router = CreateRouter(catalogDb, suppliersDb, registry);
        var readiness = await router.GetReadinessAsync(variantId, CancellationToken.None);

        Assert.False(readiness.SupplierReady);
    }

    [Fact]
    public async Task GetReadinessAsync_UnregisteredProviderType_IsSkipped()
    {
        var (_, catalogDb) = CommerceTestDbContextFactory.Create();
        var suppliersDb = SuppliersTestDbContextFactory.Create();
        var registry = new SupplierProviderRegistry([]); // nothing registered
        var productId = Guid.NewGuid();
        var variantId = await SeedVariantAsync(catalogDb, productId, FulfillmentMode.SupplierOnly);
        var supplier = await SeedSupplierAsync(suppliersDb);
        await SeedMappingAsync(suppliersDb, supplier.Id, productId);

        var router = CreateRouter(catalogDb, suppliersDb, registry);
        var readiness = await router.GetReadinessAsync(variantId, CancellationToken.None);

        Assert.False(readiness.SupplierReady);
    }

    [Fact]
    public async Task GetReadinessAsync_MissingCredentials_IsSkipped()
    {
        var (_, catalogDb) = CommerceTestDbContextFactory.Create();
        var suppliersDb = SuppliersTestDbContextFactory.Create();
        var registry = new SupplierProviderRegistry([new FakeSupplierProvider("Fake")]);
        var productId = Guid.NewGuid();
        var variantId = await SeedVariantAsync(catalogDb, productId, FulfillmentMode.SupplierOnly);

        // ApiKey auth type but no credentials ever set — HasCredentialsConfigured must be false.
        var supplier = Supplier.Create("Unconfigured", $"SUP-{Guid.NewGuid():N}", "Fake", SupplierAuthenticationType.ApiKey, null, 0);
        suppliersDb.Suppliers.Add(supplier);
        await suppliersDb.SaveChangesAsync();
        await SeedMappingAsync(suppliersDb, supplier.Id, productId);

        var router = CreateRouter(catalogDb, suppliersDb, registry);
        var readiness = await router.GetReadinessAsync(variantId, CancellationToken.None);

        Assert.False(readiness.SupplierReady);
    }

    [Fact]
    public async Task GetReadinessAsync_VariantSpecificMapping_TakesPrecedenceOverProductWideMapping()
    {
        var (_, catalogDb) = CommerceTestDbContextFactory.Create();
        var suppliersDb = SuppliersTestDbContextFactory.Create();
        var registry = new SupplierProviderRegistry([new FakeSupplierProvider("Fake")]);
        var productId = Guid.NewGuid();
        var variantId = await SeedVariantAsync(catalogDb, productId, FulfillmentMode.SupplierFirst);
        var supplier = await SeedSupplierAsync(suppliersDb);

        // Product-wide mapping created first, with the numerically-lower (higher) priority — the
        // variant-specific mapping must still win despite its worse priority number, since exact
        // variant matches are preferred before priority is even consulted.
        var productWide = await SeedMappingAsync(suppliersDb, supplier.Id, productId, variantId: null, priority: 0);
        var variantSpecific = await SeedMappingAsync(suppliersDb, supplier.Id, productId, variantId: variantId, priority: 99);

        var router = CreateRouter(catalogDb, suppliersDb, registry);
        var readiness = await router.GetReadinessAsync(variantId, CancellationToken.None);

        Assert.Equal(variantSpecific.Id, readiness.SupplierCandidate!.SupplierProductMappingId);
        Assert.NotEqual(productWide.Id, readiness.SupplierCandidate.SupplierProductMappingId);
    }

    [Fact]
    public async Task GetReadinessAsync_MultipleSuppliers_UsesMappingPriorityOrder_AndSkipsNotReadyOnes()
    {
        var (_, catalogDb) = CommerceTestDbContextFactory.Create();
        var suppliersDb = SuppliersTestDbContextFactory.Create();
        var registry = new SupplierProviderRegistry([new FakeSupplierProvider("Fake")]);
        var productId = Guid.NewGuid();
        var variantId = await SeedVariantAsync(catalogDb, productId, FulfillmentMode.SupplierFirst);

        var supplierA = await SeedSupplierAsync(suppliersDb, enabled: false); // disabled — must be skipped
        var mappingA = await SeedMappingAsync(suppliersDb, supplierA.Id, productId, priority: 0);
        var supplierB = await SeedSupplierAsync(suppliersDb);
        var mappingB = await SeedMappingAsync(suppliersDb, supplierB.Id, productId, priority: 1);

        var router = CreateRouter(catalogDb, suppliersDb, registry);
        var readiness = await router.GetReadinessAsync(variantId, CancellationToken.None);

        // Supplier A has the better (lower) priority but is disabled — the chain must fall through
        // to Supplier B rather than reporting "not ready" outright.
        Assert.Equal(supplierB.Id, readiness.SupplierCandidate!.SupplierId);
        Assert.Equal(mappingB.Id, readiness.SupplierCandidate.SupplierProductMappingId);
        Assert.NotEqual(mappingA.Id, readiness.SupplierCandidate.SupplierProductMappingId);
    }

    // ===================== FulfillmentRouter — supplier AVAILABILITY (fresh/stale/unavailable/unknown) =====================

    [Fact]
    public async Task GetReadinessAsync_ReadySupplier_FreshAvailable_ReturnsCandidate()
    {
        var (_, catalogDb) = CommerceTestDbContextFactory.Create();
        var suppliersDb = SuppliersTestDbContextFactory.Create();
        var registry = new SupplierProviderRegistry([new FakeSupplierProvider("Fake")]);
        var productId = Guid.NewGuid();
        var variantId = await SeedVariantAsync(catalogDb, productId, FulfillmentMode.SupplierOnly);
        var supplier = await SeedSupplierAsync(suppliersDb);
        // SeedMappingAsync already seeds a fresh Available row by default.
        var mapping = await SeedMappingAsync(suppliersDb, supplier.Id, productId);

        var router = CreateRouter(catalogDb, suppliersDb, registry);
        var readiness = await router.GetReadinessAsync(variantId, CancellationToken.None);

        Assert.True(readiness.SupplierReady);
        Assert.Equal(mapping.Id, readiness.SupplierCandidate!.SupplierProductMappingId);
    }

    [Fact]
    public async Task GetReadinessAsync_ReadySupplier_ButUnavailable_ReturnsNoCandidate()
    {
        var (_, catalogDb) = CommerceTestDbContextFactory.Create();
        var suppliersDb = SuppliersTestDbContextFactory.Create();
        var registry = new SupplierProviderRegistry([new FakeSupplierProvider("Fake")]);
        var productId = Guid.NewGuid();
        var variantId = await SeedVariantAsync(catalogDb, productId, FulfillmentMode.SupplierOnly);
        var supplier = await SeedSupplierAsync(suppliersDb);
        var mapping = SupplierProductMapping.Create(supplier.Id, productId, "EXT-1", null, null, 5m, "USD", 0);
        suppliersDb.SupplierProductMappings.Add(mapping);
        await suppliersDb.SaveChangesAsync();
        await SeedAvailabilityAsync(suppliersDb, supplier.Id, mapping.Id, DomainAvailabilityState.Unavailable, DateTimeOffset.UtcNow);

        var router = CreateRouter(catalogDb, suppliersDb, registry);
        var readiness = await router.GetReadinessAsync(variantId, CancellationToken.None);

        Assert.False(readiness.SupplierReady);
    }

    [Fact]
    public async Task GetReadinessAsync_ReadySupplier_ButUnknown_NoRowYet_ReturnsNoCandidate()
    {
        var (_, catalogDb) = CommerceTestDbContextFactory.Create();
        var suppliersDb = SuppliersTestDbContextFactory.Create();
        var registry = new SupplierProviderRegistry([new FakeSupplierProvider("Fake")]);
        var productId = Guid.NewGuid();
        var variantId = await SeedVariantAsync(catalogDb, productId, FulfillmentMode.SupplierOnly);
        var supplier = await SeedSupplierAsync(suppliersDb);
        // Mapping created directly (bypassing SeedMappingAsync) so NO availability row exists at all —
        // exactly the "brand-new mapping, never synced" case.
        var mapping = SupplierProductMapping.Create(supplier.Id, productId, "EXT-1", null, null, 5m, "USD", 0);
        suppliersDb.SupplierProductMappings.Add(mapping);
        await suppliersDb.SaveChangesAsync();

        var router = CreateRouter(catalogDb, suppliersDb, registry);
        var readiness = await router.GetReadinessAsync(variantId, CancellationToken.None);

        Assert.False(readiness.SupplierReady);
    }

    [Fact]
    public async Task GetReadinessAsync_ReadySupplier_StaleAvailable_ReturnsNoCandidate()
    {
        var (_, catalogDb) = CommerceTestDbContextFactory.Create();
        var suppliersDb = SuppliersTestDbContextFactory.Create();
        var registry = new SupplierProviderRegistry([new FakeSupplierProvider("Fake")]);
        var productId = Guid.NewGuid();
        var variantId = await SeedVariantAsync(catalogDb, productId, FulfillmentMode.SupplierOnly);
        var supplier = await SeedSupplierAsync(suppliersDb);
        var mapping = SupplierProductMapping.Create(supplier.Id, productId, "EXT-1", null, null, 5m, "USD", 0);
        suppliersDb.SupplierProductMappings.Add(mapping);
        await suppliersDb.SaveChangesAsync();
        // Available, but checked 30 minutes ago against a 10-minute staleness threshold.
        await SeedAvailabilityAsync(suppliersDb, supplier.Id, mapping.Id, DomainAvailabilityState.Available, DateTimeOffset.UtcNow.AddMinutes(-30));

        var router = CreateRouter(catalogDb, suppliersDb, registry, staleAfterMinutes: 10);
        var readiness = await router.GetReadinessAsync(variantId, CancellationToken.None);

        Assert.False(readiness.SupplierReady);
    }

    [Fact]
    public async Task GetReadinessAsync_ReadySupplier_AvailableJustInsideStaleThreshold_ReturnsCandidate()
    {
        var (_, catalogDb) = CommerceTestDbContextFactory.Create();
        var suppliersDb = SuppliersTestDbContextFactory.Create();
        var registry = new SupplierProviderRegistry([new FakeSupplierProvider("Fake")]);
        var productId = Guid.NewGuid();
        var variantId = await SeedVariantAsync(catalogDb, productId, FulfillmentMode.SupplierOnly);
        var supplier = await SeedSupplierAsync(suppliersDb);
        var mapping = SupplierProductMapping.Create(supplier.Id, productId, "EXT-1", null, null, 5m, "USD", 0);
        suppliersDb.SupplierProductMappings.Add(mapping);
        await suppliersDb.SaveChangesAsync();
        await SeedAvailabilityAsync(suppliersDb, supplier.Id, mapping.Id, DomainAvailabilityState.Available, DateTimeOffset.UtcNow.AddMinutes(-9));

        var router = CreateRouter(catalogDb, suppliersDb, registry, staleAfterMinutes: 10);
        var readiness = await router.GetReadinessAsync(variantId, CancellationToken.None);

        Assert.True(readiness.SupplierReady);
    }

    // ===================== CartLineValidator (checkout-time gate) =====================

    private static async Task<(HAMBOX.Modules.Commerce.Infrastructure.Persistence.CommerceDbContext CommerceDb, HAMBOX.Modules.Catalog.Infrastructure.Persistence.CatalogDbContext CatalogDb, Product Product, ProductVariant Variant, ShoppingCart Cart)>
        SeedCartAsync(int quantity, FulfillmentMode mode)
    {
        var (commerceDb, catalogDb) = CommerceTestDbContextFactory.Create();
        var category = Category.Create("فئة", "Category", $"category-{Guid.NewGuid():N}");
        var product = Product.Create("منتج", "Product", "وصف", "Description", 19.99m, category.Id);
        product.Activate();
        var variant = ProductVariant.Create(product.Id, $"SKU-{Guid.NewGuid():N}");
        variant.Activate();
        variant.SetFulfillmentMode(mode);

        catalogDb.Categories.Add(category);
        catalogDb.Products.Add(product);
        catalogDb.ProductVariants.Add(variant);
        await catalogDb.SaveChangesAsync();

        var cart = ShoppingCart.CreateForUser("user-1");
        cart.AddOrUpdateItem(product.Id, quantity, product.Price, variant.Id);
        commerceDb.ShoppingCarts.Add(cart);
        await commerceDb.SaveChangesAsync();

        return (commerceDb, catalogDb, product, variant, cart);
    }

    private static async Task<Result> ValidateAsync(
        HAMBOX.Modules.Catalog.Infrastructure.Persistence.CatalogDbContext catalogDb,
        Product product,
        ProductVariant variant,
        ShoppingCart cart,
        int availableStock,
        IFulfillmentRouter router)
    {
        var validator = new CartLineValidator(new FakeInventoryEngine(catalogDb), router);
        var products = new Dictionary<Guid, Product> { [product.Id] = product };
        var variants = new Dictionary<Guid, ProductVariant> { [variant.Id] = variant };
        var stock = new Dictionary<Guid, VariantStockSnapshot>
        {
            [variant.Id] = new(variant.Id, availableStock, 0, 0, 0, 0, false, availableStock <= 0),
        };
        var access = new Dictionary<Guid, ProductAccessInfo>();

        return await validator.ValidateAsync(cart, products, variants, stock, access, MembershipAccessInfo.None, CancellationToken.None);
    }

    [Fact]
    public async Task CartLineValidator_SupplierOnly_ZeroManualStock_ReadySupplier_Passes()
    {
        var (_, catalogDb, product, variant, cart) = await SeedCartAsync(3, FulfillmentMode.SupplierOnly);
        var router = new FakeFulfillmentRouter();
        router.SetReadiness(variant.Id, new FulfillmentReadiness(FulfillmentMode.SupplierOnly, false, new FulfillmentSupplierCandidate(Guid.NewGuid(), Guid.NewGuid())));

        var result = await ValidateAsync(catalogDb, product, variant, cart, availableStock: 0, router);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task CartLineValidator_SupplierOnly_NoReadySupplier_Fails()
    {
        var (_, catalogDb, product, variant, cart) = await SeedCartAsync(3, FulfillmentMode.SupplierOnly);
        var router = new FakeFulfillmentRouter();
        router.SetReadiness(variant.Id, new FulfillmentReadiness(FulfillmentMode.SupplierOnly, false, null));

        var result = await ValidateAsync(catalogDb, product, variant, cart, availableStock: 100, router);

        // Even though manual stock genuinely exists, SupplierOnly must never use it as a substitute
        // for a READY supplier — this line must be rejected at checkout, not silently downgraded.
        Assert.True(result.IsFailure);
        Assert.Equal(CatalogErrors.InsufficientInventory.Code, result.Error.Code);
    }

    [Fact]
    public async Task CartLineValidator_SupplierFirst_ManualStockSufficientButNoReadySupplier_StillFails()
    {
        var (_, catalogDb, product, variant, cart) = await SeedCartAsync(1, FulfillmentMode.SupplierFirst);
        var router = new FakeFulfillmentRouter();
        router.SetReadiness(variant.Id, new FulfillmentReadiness(FulfillmentMode.SupplierFirst, false, null));

        var result = await ValidateAsync(catalogDb, product, variant, cart, availableStock: 50, router);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task CartLineValidator_ManualFirst_InsufficientStock_ReadySupplierCoversIt_Passes()
    {
        var (_, catalogDb, product, variant, cart) = await SeedCartAsync(5, FulfillmentMode.ManualFirst);
        var router = new FakeFulfillmentRouter();
        router.SetReadiness(variant.Id, new FulfillmentReadiness(FulfillmentMode.ManualFirst, true, new FulfillmentSupplierCandidate(Guid.NewGuid(), Guid.NewGuid())));

        var result = await ValidateAsync(catalogDb, product, variant, cart, availableStock: 2, router);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task CartLineValidator_ManualOnly_InsufficientStock_Fails_RegardlessOfSupplier()
    {
        var (_, catalogDb, product, variant, cart) = await SeedCartAsync(5, FulfillmentMode.ManualOnly);
        var router = new FakeFulfillmentRouter();
        // Even a ready supplier candidate must not rescue a ManualOnly line.
        router.SetReadiness(variant.Id, new FulfillmentReadiness(FulfillmentMode.ManualOnly, true, new FulfillmentSupplierCandidate(Guid.NewGuid(), Guid.NewGuid())));

        var result = await ValidateAsync(catalogDb, product, variant, cart, availableStock: 0, router);

        Assert.True(result.IsFailure);
    }

    // ===================== End-to-end mode dispatch (OrderFulfillmentService + delivery sink) =====================

    private sealed record Harness(
        OrderFulfillmentService FulfillmentService,
        FakeSupplierProvider Provider,
        FakeInventoryEngine InventoryEngine,
        HAMBOX.Modules.Suppliers.Infrastructure.Persistence.SuppliersDbContext SuppliersDb,
        HAMBOX.Modules.Commerce.Infrastructure.Persistence.CommerceDbContext CommerceDb,
        HAMBOX.Modules.Catalog.Infrastructure.Persistence.CatalogDbContext CatalogDb);

    private static Harness CreateHarness()
    {
        var (commerceDb, catalogDb) = CommerceTestDbContextFactory.Create();
        var suppliersDb = SuppliersTestDbContextFactory.Create();
        var provider = new FakeSupplierProvider("Fake");
        var registry = new SupplierProviderRegistry([provider]);
        var inventoryEngine = new FakeInventoryEngine(catalogDb);
        var deliverySink = new CommerceOrderLicenseKeyDeliverySink(
            commerceDb, catalogDb, inventoryEngine, new FakeCommerceTransactionService(),
            NullLogger<CommerceOrderLicenseKeyDeliverySink>.Instance);
        var supplierFulfillmentService = new HAMBOX.Modules.Suppliers.Application.Services.SupplierFulfillmentService(
            suppliersDb, registry, NullLogger<HAMBOX.Modules.Suppliers.Application.Services.SupplierFulfillmentService>.Instance, deliverySink);
        var router = CreateRouter(catalogDb, suppliersDb, registry);
        var service = new OrderFulfillmentService(
            commerceDb, inventoryEngine, supplierFulfillmentService, router, NullLogger<OrderFulfillmentService>.Instance);

        return new Harness(service, provider, inventoryEngine, suppliersDb, commerceDb, catalogDb);
    }

    private static Order CreatePaidOrder(Guid productId, Guid variantId, int quantity)
    {
        var order = Order.Create(
            userId: "user-1", orderNumber: $"ORD-{Guid.NewGuid():N}", email: "buyer@example.com", country: "US",
            paymentMethod: "development", subtotal: 10m, discountAmount: 0m, taxAmount: 0m, totalAmount: 10m,
            items: [(productId, "Test Product", quantity, 10m, (Guid?)variantId, (string?)null)]);
        order.RecordPayment("development", $"txn-{Guid.NewGuid():N}");
        return order;
    }

    [Fact]
    public async Task SupplierOnly_NeverConsumesManualInventory_EvenWhenAvailable()
    {
        var h = CreateHarness();
        var productId = Guid.NewGuid();
        var variantId = await SeedVariantAsync(h.CatalogDb, productId, FulfillmentMode.SupplierOnly);
        var supplier = await SeedSupplierAsync(h.SuppliersDb);
        await SeedMappingAsync(h.SuppliersDb, supplier.Id, productId, variantId);

        h.InventoryEngine.AvailableStockByVariant[variantId] = 10; // plenty of manual stock

        var order = CreatePaidOrder(productId, variantId, 3);
        h.CommerceDb.Orders.Add(order);
        await h.CommerceDb.SaveChangesAsync();

        // FulfillMissingAsync is the manual path — for SupplierOnly it must be a strict no-op.
        var manualResult = await h.FulfillmentService.FulfillMissingAsync(order, CancellationToken.None);
        Assert.Equal(0, manualResult.CodesDelivered);
        Assert.Equal(10, h.InventoryEngine.AvailableStockByVariant[variantId]); // untouched

        var summary = await h.FulfillmentService.QueueAutomatedSupplierFulfillmentAsync(order, CancellationToken.None);

        Assert.Equal(1, summary.Queued);
        Assert.Single(h.Provider.PurchaseCalls);
        Assert.Equal(3, h.Provider.PurchaseCalls[0].Quantity); // full quantity, not a shortfall
        Assert.Equal(10, h.InventoryEngine.AvailableStockByVariant[variantId]); // still untouched
    }

    [Fact]
    public async Task SupplierFirst_Success_ManualInventoryNeverTouched()
    {
        var h = CreateHarness();
        var productId = Guid.NewGuid();
        var variantId = await SeedVariantAsync(h.CatalogDb, productId, FulfillmentMode.SupplierFirst);
        var supplier = await SeedSupplierAsync(h.SuppliersDb);
        await SeedMappingAsync(h.SuppliersDb, supplier.Id, productId, variantId);
        h.InventoryEngine.AvailableStockByVariant[variantId] = 5;

        var order = CreatePaidOrder(productId, variantId, 2);
        h.CommerceDb.Orders.Add(order);
        await h.CommerceDb.SaveChangesAsync();

        await h.FulfillmentService.QueueAutomatedSupplierFulfillmentAsync(order, CancellationToken.None);

        Assert.Equal(5, h.InventoryEngine.AvailableStockByVariant[variantId]);
        Assert.Equal(2, await h.CommerceDb.OrderLicenseKeys.CountAsync(k => k.OrderId == order.Id));
    }

    [Fact]
    public async Task SupplierFirst_DefinitiveFailure_ManualFallbackDeliversRemainder()
    {
        var h = CreateHarness();
        var productId = Guid.NewGuid();
        var variantId = await SeedVariantAsync(h.CatalogDb, productId, FulfillmentMode.SupplierFirst);
        var supplier = await SeedSupplierAsync(h.SuppliersDb);
        await SeedMappingAsync(h.SuppliersDb, supplier.Id, productId, variantId);
        h.InventoryEngine.AvailableStockByVariant[variantId] = 5;
        h.InventoryEngine.CodesByVariant[variantId] = new Queue<string>(["MANUAL-FALLBACK-CODE"]);

        // A definite, terminal rejection (e.g. InsufficientSupplierBalance) — never ambiguous.
        h.Provider.PurchaseResponder = (req, ctx) =>
            new SupplierPurchaseResult(false, null, null, HAMBOX.Modules.Suppliers.Domain.Fulfillments.SupplierFulfillmentFailureCategory.InsufficientSupplierBalance, "no balance");

        var order = CreatePaidOrder(productId, variantId, 1);
        h.CommerceDb.Orders.Add(order);
        await h.CommerceDb.SaveChangesAsync();

        await h.FulfillmentService.QueueAutomatedSupplierFulfillmentAsync(order, CancellationToken.None);

        // Failed is immediately exhausted (no automatic follow-up exists for a pure Failed outcome) —
        // the manual fallback must fire on this very first terminal notification.
        Assert.Equal(1, await h.CommerceDb.OrderLicenseKeys.CountAsync(k => k.OrderId == order.Id));
        Assert.Equal(4, h.InventoryEngine.AvailableStockByVariant[variantId]);
    }

    [Fact]
    public async Task SupplierOnly_DefinitiveFailure_NeverFallsBackToManual()
    {
        var h = CreateHarness();
        var productId = Guid.NewGuid();
        var variantId = await SeedVariantAsync(h.CatalogDb, productId, FulfillmentMode.SupplierOnly);
        var supplier = await SeedSupplierAsync(h.SuppliersDb);
        await SeedMappingAsync(h.SuppliersDb, supplier.Id, productId, variantId);
        h.InventoryEngine.AvailableStockByVariant[variantId] = 5;
        h.InventoryEngine.CodesByVariant[variantId] = new Queue<string>(["SHOULD-NEVER-BE-USED"]);

        h.Provider.PurchaseResponder = (req, ctx) =>
            new SupplierPurchaseResult(false, null, null, HAMBOX.Modules.Suppliers.Domain.Fulfillments.SupplierFulfillmentFailureCategory.ProductUnavailable, "out of stock");

        var order = CreatePaidOrder(productId, variantId, 1);
        h.CommerceDb.Orders.Add(order);
        await h.CommerceDb.SaveChangesAsync();

        await h.FulfillmentService.QueueAutomatedSupplierFulfillmentAsync(order, CancellationToken.None);

        // SupplierOnly must NEVER fall back to manual, even after a definite, exhausted failure.
        Assert.Equal(0, await h.CommerceDb.OrderLicenseKeys.CountAsync(k => k.OrderId == order.Id));
        Assert.Equal(5, h.InventoryEngine.AvailableStockByVariant[variantId]);
    }

    [Fact]
    public async Task SupplierFirst_AmbiguousUnknown_NeverTriggersManualFallback()
    {
        var h = CreateHarness();
        var productId = Guid.NewGuid();
        var variantId = await SeedVariantAsync(h.CatalogDb, productId, FulfillmentMode.SupplierFirst);
        var supplier = await SeedSupplierAsync(h.SuppliersDb);
        await SeedMappingAsync(h.SuppliersDb, supplier.Id, productId, variantId);
        h.InventoryEngine.AvailableStockByVariant[variantId] = 5;
        h.InventoryEngine.CodesByVariant[variantId] = new Queue<string>(["SHOULD-NEVER-BE-USED"]);

        // Timeout/ambiguous — the provider throws, which is exactly the "unknown, do not retry blindly" contract.
        h.Provider.PurchaseThrows = (req, ctx) => new HttpRequestException("simulated timeout");

        var order = CreatePaidOrder(productId, variantId, 1);
        h.CommerceDb.Orders.Add(order);
        await h.CommerceDb.SaveChangesAsync();

        await h.FulfillmentService.QueueAutomatedSupplierFulfillmentAsync(order, CancellationToken.None);

        Assert.Equal(0, await h.CommerceDb.OrderLicenseKeys.CountAsync(k => k.OrderId == order.Id));
        Assert.Equal(5, h.InventoryEngine.AvailableStockByVariant[variantId]); // manual never touched
        var fulfillment = await h.SuppliersDb.SupplierFulfillments.SingleAsync(f => f.OrderId == order.Id);
        Assert.Equal(HAMBOX.Modules.Suppliers.Domain.Fulfillments.SupplierFulfillmentStatus.Unknown, fulfillment.Status);
    }

    [Fact]
    public async Task SupplierFirst_PartialFailed_FirstAttempt_NoManualFallbackYet()
    {
        var h = CreateHarness();
        var productId = Guid.NewGuid();
        var variantId = await SeedVariantAsync(h.CatalogDb, productId, FulfillmentMode.SupplierFirst);
        var supplier = await SeedSupplierAsync(h.SuppliersDb);
        await SeedMappingAsync(h.SuppliersDb, supplier.Id, productId, variantId);
        h.InventoryEngine.AvailableStockByVariant[variantId] = 5;
        h.InventoryEngine.CodesByVariant[variantId] = new Queue<string>(["SHOULD-NOT-BE-USED-YET"]);

        // Delivers 1 of 3 — a genuine PartialFailed. Scope attempt count is 1 of the automatic cap
        // (3), so the sweep's own follow-up mechanism may still create another attempt.
        h.Provider.PurchaseResponder = (req, ctx) => new SupplierPurchaseResult(true, "PROV-1", ["CODE-1"], null, null);

        var order = CreatePaidOrder(productId, variantId, 3);
        h.CommerceDb.Orders.Add(order);
        await h.CommerceDb.SaveChangesAsync();

        await h.FulfillmentService.QueueAutomatedSupplierFulfillmentAsync(order, CancellationToken.None);

        // The 1 supplier-delivered code was attached, but the manual fallback must NOT have fired yet
        // — only 1 attempt exists in this scope, below the automatic-follow-up cap.
        Assert.Equal(1, await h.CommerceDb.OrderLicenseKeys.CountAsync(k => k.OrderId == order.Id));
        Assert.Equal(5, h.InventoryEngine.AvailableStockByVariant[variantId]);
    }

    [Fact]
    public async Task SetVariantFulfillmentModeCommandHandler_ChangesModeAndWritesAuditEntry()
    {
        var (_, catalogDb) = CommerceTestDbContextFactory.Create();
        var category = Category.Create("فئة", "Category", $"category-{Guid.NewGuid():N}");
        var product = Product.Create("منتج", "Product", "وصف", "Description", 19.99m, category.Id);
        var variant = ProductVariant.Create(product.Id, $"SKU-{Guid.NewGuid():N}");
        catalogDb.Categories.Add(category);
        catalogDb.Products.Add(product);
        catalogDb.ProductVariants.Add(variant);
        await catalogDb.SaveChangesAsync();

        var handler = new HAMBOX.Modules.Catalog.Application.Features.Inventory.SetVariantFulfillmentModeCommandHandler(
            catalogDb, new FakeCurrentUserService("admin-1"));

        var result = await handler.Handle(
            new HAMBOX.Modules.Catalog.Application.Features.Inventory.SetVariantFulfillmentModeCommand(variant.Id, FulfillmentMode.SupplierOnly),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var reloaded = await catalogDb.ProductVariants.AsNoTracking().FirstAsync(v => v.Id == variant.Id);
        Assert.Equal(FulfillmentMode.SupplierOnly, reloaded.FulfillmentMode);

        var auditEntry = await catalogDb.InventoryAuditLogs.SingleAsync(a => a.VariantId == variant.Id);
        Assert.Equal(HAMBOX.Modules.Catalog.Domain.Enums.InventoryAuditAction.FulfillmentModeChanged, auditEntry.Action);
        Assert.Contains("ManualOnly", auditEntry.Details);
        Assert.Contains("SupplierOnly", auditEntry.Details);
    }
}
