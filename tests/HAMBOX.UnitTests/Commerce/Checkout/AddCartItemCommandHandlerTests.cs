using HAMBOX.Application.Fulfillment;
using HAMBOX.Modules.Catalog.Application.Errors;
using HAMBOX.Modules.Catalog.Domain.Categories;
using HAMBOX.Modules.Catalog.Domain.Enums;
using HAMBOX.Modules.Catalog.Domain.Inventory;
using HAMBOX.Modules.Catalog.Domain.Products;
using HAMBOX.Modules.Commerce.Application.Errors;
using HAMBOX.Modules.Commerce.Application.Features.Cart.AddCartItem;
using HAMBOX.Modules.Commerce.Application.Services;
using HAMBOX.Modules.Suppliers.Application.Options;
using HAMBOX.Modules.Suppliers.Domain.Suppliers;
using HAMBOX.Modules.Suppliers.Infrastructure.Services;
using HAMBOX.UnitTests.Commerce.TestDoubles;
using HAMBOX.UnitTests.Suppliers.TestDoubles;
using Microsoft.Extensions.Options;

namespace HAMBOX.UnitTests.Commerce.Checkout;

/// <summary>
/// H1 fix: a product with no active, visible variant must never be added to the cart — the
/// legacy Product.StockQuantity counter is CSV-import bookkeeping only, not a real deliverable,
/// so it must no longer gate a purchase.
/// </summary>
public sealed class AddCartItemCommandHandlerTests
{
    private static (Product Product, Category Category) CreateActiveProduct()
    {
        var category = Category.Create("فئة", "Category", $"category-{Guid.NewGuid():N}");
        var product = Product.Create("منتج", "Product", "وصف", "Description", 19.99m, category.Id);
        product.Activate();
        return (product, category);
    }

    private static ProductVariant CreateActiveVariant(Guid productId)
    {
        var variant = ProductVariant.Create(productId, $"SKU-{Guid.NewGuid():N}");
        variant.Activate();
        return variant;
    }

    private static AddCartItemCommandHandler CreateHandler(
        HAMBOX.Modules.Commerce.Infrastructure.Persistence.CommerceDbContext commerceDb,
        HAMBOX.Modules.Catalog.Infrastructure.Persistence.CatalogDbContext catalogDb,
        FakeInventoryEngine inventoryEngine,
        FakeCurrentUserService currentUser,
        FakeFulfillmentRouter? fulfillmentRouter = null)
    {
        var cartResponseBuilder = new CartResponseBuilder(
            commerceDb, catalogDb, new FakePromotionEngine(), new FakeMembershipEngine(), currentUser);

        return new AddCartItemCommandHandler(
            commerceDb, catalogDb, currentUser, inventoryEngine, fulfillmentRouter ?? new FakeFulfillmentRouter(),
            cartResponseBuilder, new FakeMembershipAccessProvider());
    }

    [Fact]
    public async Task Handle_VariantBackedProductWithStock_AddsItemAndSucceeds()
    {
        var (commerceDb, catalogDb) = CommerceTestDbContextFactory.Create();
        var (product, category) = CreateActiveProduct();
        var variant = CreateActiveVariant(product.Id);

        catalogDb.Categories.Add(category);
        catalogDb.Products.Add(product);
        catalogDb.ProductVariants.Add(variant);
        await catalogDb.SaveChangesAsync(CancellationToken.None);

        var inventoryEngine = new FakeInventoryEngine(catalogDb);
        inventoryEngine.AvailableStockByVariant[variant.Id] = 5;

        var currentUser = new FakeCurrentUserService(userId: "user-1");
        var handler = CreateHandler(commerceDb, catalogDb, inventoryEngine, currentUser);

        var result = await handler.Handle(
            new AddCartItemCommand(product.Id, Quantity: 1, GuestSessionId: null, ProductVariantId: variant.Id),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value.Items);
        // Add-to-cart only checks availability — HAMBOX reserves inventory at checkout time
        // (IInventoryEngine.ReserveCodesAsync), not on add-to-cart — so stock is unchanged here.
        Assert.Equal(5, inventoryEngine.AvailableStockByVariant[variant.Id]);
    }

    [Fact]
    public async Task Handle_VariantBackedProductWithInsufficientStock_ReturnsInsufficientInventory()
    {
        var (commerceDb, catalogDb) = CommerceTestDbContextFactory.Create();
        var (product, category) = CreateActiveProduct();
        var variant = CreateActiveVariant(product.Id);

        catalogDb.Categories.Add(category);
        catalogDb.Products.Add(product);
        catalogDb.ProductVariants.Add(variant);
        await catalogDb.SaveChangesAsync(CancellationToken.None);

        var inventoryEngine = new FakeInventoryEngine(catalogDb);
        inventoryEngine.AvailableStockByVariant[variant.Id] = 0;

        var currentUser = new FakeCurrentUserService(userId: "user-1");
        var handler = CreateHandler(commerceDb, catalogDb, inventoryEngine, currentUser);

        var result = await handler.Handle(
            new AddCartItemCommand(product.Id, Quantity: 1, GuestSessionId: null, ProductVariantId: variant.Id),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(CatalogErrors.InsufficientInventoryQuantity(0, 1, 0).Code, result.Error.Code);
    }

    [Fact]
    public async Task Handle_ProductWithNoVariantAtAll_IsRejectedAsNotPurchasable_NoFallbackToLegacyStock()
    {
        var (commerceDb, catalogDb) = CommerceTestDbContextFactory.Create();
        var (product, category) = CreateActiveProduct();

        // Deliberately no ProductVariant is created — this mirrors an admin publishing a product
        // (SetInitialStock(100) runs inside Product.Create) without ever adding a real,
        // inventory-backed variant. Before the H1 fix this fell through to the legacy
        // Product.AvailableStock check (100, by default) and succeeded.
        catalogDb.Categories.Add(category);
        catalogDb.Products.Add(product);
        await catalogDb.SaveChangesAsync(CancellationToken.None);

        var inventoryEngine = new FakeInventoryEngine(catalogDb);
        var currentUser = new FakeCurrentUserService(userId: "user-1");
        var handler = CreateHandler(commerceDb, catalogDb, inventoryEngine, currentUser);

        var result = await handler.Handle(
            new AddCartItemCommand(product.Id, Quantity: 1, GuestSessionId: null, ProductVariantId: null),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(CatalogErrors.ProductNotPurchasable.Code, result.Error.Code);
        Assert.Empty(commerceDb.ShoppingCarts.SelectMany(c => c.Items));
    }

    [Fact]
    public async Task Handle_ProductWithOnlyDraftVariant_IsRejectedAsNotPurchasable()
    {
        var (commerceDb, catalogDb) = CommerceTestDbContextFactory.Create();
        var (product, category) = CreateActiveProduct();

        // A variant exists but was never activated/made visible — ProductHasVariantsAsync
        // correctly reports "no purchasable variant" for this product too.
        var draftVariant = ProductVariant.Create(product.Id, $"SKU-{Guid.NewGuid():N}");

        catalogDb.Categories.Add(category);
        catalogDb.Products.Add(product);
        catalogDb.ProductVariants.Add(draftVariant);
        await catalogDb.SaveChangesAsync(CancellationToken.None);

        var inventoryEngine = new FakeInventoryEngine(catalogDb);
        var currentUser = new FakeCurrentUserService(userId: "user-1");
        var handler = CreateHandler(commerceDb, catalogDb, inventoryEngine, currentUser);

        var result = await handler.Handle(
            new AddCartItemCommand(product.Id, Quantity: 1, GuestSessionId: null, ProductVariantId: null),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(CatalogErrors.ProductNotPurchasable.Code, result.Error.Code);
    }

    /// <summary>
    /// Variant lifecycle: an Archived variant (the primary, reversible "take this off sale" admin
    /// action — see ProductVariant.Archive) must be rejected exactly like a never-activated Draft
    /// one, not silently added because it "used to be" Active.
    /// </summary>
    [Fact]
    public async Task Handle_ArchivedVariant_IsRejectedAsNotFound_CannotBePurchased()
    {
        var (commerceDb, catalogDb) = CommerceTestDbContextFactory.Create();
        var (product, category) = CreateActiveProduct();
        var variant = CreateActiveVariant(product.Id);
        variant.Archive();

        catalogDb.Categories.Add(category);
        catalogDb.Products.Add(product);
        catalogDb.ProductVariants.Add(variant);
        await catalogDb.SaveChangesAsync(CancellationToken.None);

        var inventoryEngine = new FakeInventoryEngine(catalogDb);
        inventoryEngine.AvailableStockByVariant[variant.Id] = 5;
        var currentUser = new FakeCurrentUserService(userId: "user-1");
        var handler = CreateHandler(commerceDb, catalogDb, inventoryEngine, currentUser);

        var result = await handler.Handle(
            new AddCartItemCommand(product.Id, Quantity: 1, GuestSessionId: null, ProductVariantId: variant.Id),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(CatalogErrors.VariantNotFound.Code, result.Error.Code);
        Assert.Empty(commerceDb.ShoppingCarts.SelectMany(c => c.Items));
    }

    /// <summary>
    /// Regression test for the reported bug, reproduced live against a real Bamboo-mapped product
    /// (see the investigation's DB verification): a SupplierFirst/SupplierOnly variant with zero
    /// manual codes but a READY automated-supplier route must be addable to the cart — before this
    /// fix, this call returned 400 InsufficientInventory even though the storefront correctly showed
    /// it as in stock, because AddCartItemCommandHandler only ever consulted manual stock.
    /// </summary>
    [Fact]
    public async Task Handle_SupplierOnly_ZeroManualStock_ReadySupplier_Succeeds()
    {
        var (commerceDb, catalogDb) = CommerceTestDbContextFactory.Create();
        var (product, category) = CreateActiveProduct();
        var variant = CreateActiveVariant(product.Id);
        variant.SetFulfillmentMode(FulfillmentMode.SupplierOnly);

        catalogDb.Categories.Add(category);
        catalogDb.Products.Add(product);
        catalogDb.ProductVariants.Add(variant);
        await catalogDb.SaveChangesAsync(CancellationToken.None);

        var inventoryEngine = new FakeInventoryEngine(catalogDb); // 0 manual stock
        var router = new FakeFulfillmentRouter();
        router.SetReadiness(variant.Id, new FulfillmentReadiness(
            FulfillmentMode.SupplierOnly, false, new FulfillmentSupplierCandidate(Guid.NewGuid(), Guid.NewGuid())));
        var currentUser = new FakeCurrentUserService(userId: "user-1");
        var handler = CreateHandler(commerceDb, catalogDb, inventoryEngine, currentUser, router);

        var result = await handler.Handle(
            new AddCartItemCommand(product.Id, Quantity: 1, GuestSessionId: null, ProductVariantId: variant.Id),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value.Items);
    }

    [Fact]
    public async Task Handle_SupplierOnly_SupplierNotReady_StillFails()
    {
        var (commerceDb, catalogDb) = CommerceTestDbContextFactory.Create();
        var (product, category) = CreateActiveProduct();
        var variant = CreateActiveVariant(product.Id);
        variant.SetFulfillmentMode(FulfillmentMode.SupplierOnly);

        catalogDb.Categories.Add(category);
        catalogDb.Products.Add(product);
        catalogDb.ProductVariants.Add(variant);
        await catalogDb.SaveChangesAsync(CancellationToken.None);

        var inventoryEngine = new FakeInventoryEngine(catalogDb);
        var router = new FakeFulfillmentRouter();
        router.SetReadiness(variant.Id, new FulfillmentReadiness(FulfillmentMode.SupplierOnly, false, null));
        var currentUser = new FakeCurrentUserService(userId: "user-1");
        var handler = CreateHandler(commerceDb, catalogDb, inventoryEngine, currentUser, router);

        var result = await handler.Handle(
            new AddCartItemCommand(product.Id, Quantity: 1, GuestSessionId: null, ProductVariantId: variant.Id),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(CatalogErrors.InsufficientInventoryQuantity(0, 1, 0).Code, result.Error.Code);
    }

    // ===================== Server-side enforcement against the REAL FulfillmentRouter =====================
    // Everything above uses FakeFulfillmentRouter to isolate this handler's own logic. These use the
    // real production router + a real persisted SupplierProductAvailability row, proving end-to-end that
    // "Add to Cart" is blocked at the server boundary — not merely hidden by the storefront UI — for
    // every one of the availability states the phase introduces (Available/Unavailable/stale/Unknown).

    private static AddCartItemCommandHandler CreateHandlerWithRealRouter(
        HAMBOX.Modules.Commerce.Infrastructure.Persistence.CommerceDbContext commerceDb,
        HAMBOX.Modules.Catalog.Infrastructure.Persistence.CatalogDbContext catalogDb,
        HAMBOX.Modules.Suppliers.Infrastructure.Persistence.SuppliersDbContext suppliersDb,
        FakeInventoryEngine inventoryEngine,
        FakeCurrentUserService currentUser,
        int staleAfterMinutes = 10)
    {
        var router = new FulfillmentRouter(
            catalogDb, suppliersDb, new SupplierProviderRegistry([new FakeSupplierProvider("Fake")]),
            Options.Create(new SupplierAvailabilityOptions { StaleAfterMinutes = staleAfterMinutes }));
        var cartResponseBuilder = new CartResponseBuilder(
            commerceDb, catalogDb, new FakePromotionEngine(), new FakeMembershipEngine(), currentUser);
        return new AddCartItemCommandHandler(
            commerceDb, catalogDb, currentUser, inventoryEngine, router, cartResponseBuilder, new FakeMembershipAccessProvider());
    }

    [Fact]
    public async Task Handle_RealRouter_SupplierOnly_UnavailableInPersistedCache_RejectsAtServerBoundary()
    {
        var (commerceDb, catalogDb) = CommerceTestDbContextFactory.Create();
        var suppliersDb = SuppliersTestDbContextFactory.Create();
        var (product, category) = CreateActiveProduct();
        var variant = CreateActiveVariant(product.Id);
        variant.SetFulfillmentMode(FulfillmentMode.SupplierOnly);
        catalogDb.Categories.Add(category);
        catalogDb.Products.Add(product);
        catalogDb.ProductVariants.Add(variant);
        await catalogDb.SaveChangesAsync(CancellationToken.None);

        var supplier = Supplier.Create("Fake Supplier", $"SUP-{Guid.NewGuid():N}", "Fake", SupplierAuthenticationType.None, null, 0);
        suppliersDb.Suppliers.Add(supplier);
        var mapping = SupplierProductMapping.Create(supplier.Id, product.Id, "EXT-1", null, null, 5m, "USD", 0, variant.Id);
        suppliersDb.SupplierProductMappings.Add(mapping);
        await suppliersDb.SaveChangesAsync(CancellationToken.None);
        var availability = SupplierProductAvailability.CreateUnknown(supplier.Id, mapping.Id, "EXT-1");
        availability.RecordChecked(SupplierAvailabilityState.Unavailable, null, DateTimeOffset.UtcNow, "EXT-1");
        suppliersDb.SupplierProductAvailabilities.Add(availability);
        await suppliersDb.SaveChangesAsync(CancellationToken.None);

        var inventoryEngine = new FakeInventoryEngine(catalogDb); // 0 manual stock
        var currentUser = new FakeCurrentUserService(userId: "user-1");
        var handler = CreateHandlerWithRealRouter(commerceDb, catalogDb, suppliersDb, inventoryEngine, currentUser);

        var result = await handler.Handle(
            new AddCartItemCommand(product.Id, Quantity: 1, GuestSessionId: null, ProductVariantId: variant.Id),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Empty(commerceDb.ShoppingCarts.SelectMany(c => c.Items));
    }

    [Fact]
    public async Task Handle_RealRouter_SupplierOnly_StaleAvailability_RejectsAtServerBoundary()
    {
        var (commerceDb, catalogDb) = CommerceTestDbContextFactory.Create();
        var suppliersDb = SuppliersTestDbContextFactory.Create();
        var (product, category) = CreateActiveProduct();
        var variant = CreateActiveVariant(product.Id);
        variant.SetFulfillmentMode(FulfillmentMode.SupplierOnly);
        catalogDb.Categories.Add(category);
        catalogDb.Products.Add(product);
        catalogDb.ProductVariants.Add(variant);
        await catalogDb.SaveChangesAsync(CancellationToken.None);

        var supplier = Supplier.Create("Fake Supplier", $"SUP-{Guid.NewGuid():N}", "Fake", SupplierAuthenticationType.None, null, 0);
        suppliersDb.Suppliers.Add(supplier);
        var mapping = SupplierProductMapping.Create(supplier.Id, product.Id, "EXT-1", null, null, 5m, "USD", 0, variant.Id);
        suppliersDb.SupplierProductMappings.Add(mapping);
        await suppliersDb.SaveChangesAsync(CancellationToken.None);
        var availability = SupplierProductAvailability.CreateUnknown(supplier.Id, mapping.Id, "EXT-1");
        // Available, but the check happened 1 hour ago against a 10-minute freshness window.
        availability.RecordChecked(SupplierAvailabilityState.Available, null, DateTimeOffset.UtcNow.AddHours(-1), "EXT-1");
        suppliersDb.SupplierProductAvailabilities.Add(availability);
        await suppliersDb.SaveChangesAsync(CancellationToken.None);

        var inventoryEngine = new FakeInventoryEngine(catalogDb);
        var currentUser = new FakeCurrentUserService(userId: "user-1");
        var handler = CreateHandlerWithRealRouter(commerceDb, catalogDb, suppliersDb, inventoryEngine, currentUser, staleAfterMinutes: 10);

        var result = await handler.Handle(
            new AddCartItemCommand(product.Id, Quantity: 1, GuestSessionId: null, ProductVariantId: variant.Id),
            CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task Handle_RealRouter_SupplierOnly_FreshAvailable_SucceedsAtServerBoundary()
    {
        var (commerceDb, catalogDb) = CommerceTestDbContextFactory.Create();
        var suppliersDb = SuppliersTestDbContextFactory.Create();
        var (product, category) = CreateActiveProduct();
        var variant = CreateActiveVariant(product.Id);
        variant.SetFulfillmentMode(FulfillmentMode.SupplierOnly);
        catalogDb.Categories.Add(category);
        catalogDb.Products.Add(product);
        catalogDb.ProductVariants.Add(variant);
        await catalogDb.SaveChangesAsync(CancellationToken.None);

        var supplier = Supplier.Create("Fake Supplier", $"SUP-{Guid.NewGuid():N}", "Fake", SupplierAuthenticationType.None, null, 0);
        suppliersDb.Suppliers.Add(supplier);
        var mapping = SupplierProductMapping.Create(supplier.Id, product.Id, "EXT-1", null, null, 5m, "USD", 0, variant.Id);
        suppliersDb.SupplierProductMappings.Add(mapping);
        await suppliersDb.SaveChangesAsync(CancellationToken.None);
        var availability = SupplierProductAvailability.CreateUnknown(supplier.Id, mapping.Id, "EXT-1");
        availability.RecordChecked(SupplierAvailabilityState.Available, null, DateTimeOffset.UtcNow, "EXT-1");
        suppliersDb.SupplierProductAvailabilities.Add(availability);
        await suppliersDb.SaveChangesAsync(CancellationToken.None);

        var inventoryEngine = new FakeInventoryEngine(catalogDb);
        var currentUser = new FakeCurrentUserService(userId: "user-1");
        var handler = CreateHandlerWithRealRouter(commerceDb, catalogDb, suppliersDb, inventoryEngine, currentUser);

        var result = await handler.Handle(
            new AddCartItemCommand(product.Id, Quantity: 1, GuestSessionId: null, ProductVariantId: variant.Id),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
    }
}
