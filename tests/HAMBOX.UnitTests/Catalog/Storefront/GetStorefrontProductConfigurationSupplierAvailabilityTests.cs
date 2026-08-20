using HAMBOX.Application.Fulfillment;
using HAMBOX.Modules.Catalog.Application.Errors;
using HAMBOX.Modules.Catalog.Application.Features.Storefront.GetProductConfiguration;
using HAMBOX.Modules.Catalog.Domain.Categories;
using HAMBOX.Modules.Catalog.Domain.Enums;
using HAMBOX.Modules.Catalog.Domain.Inventory;
using HAMBOX.Modules.Catalog.Domain.Products;
using HAMBOX.Modules.Commerce.Application.Services;
using HAMBOX.Modules.Commerce.Domain.Account;
using HAMBOX.Modules.Commerce.Domain.Carts;
using HAMBOX.SharedKernel.Results;
using HAMBOX.UnitTests.Commerce.TestDoubles;
using Microsoft.Extensions.Logging.Abstractions;

namespace HAMBOX.UnitTests.Catalog.Storefront;

/// <summary>
/// Reproduces the reported bug end-to-end: a variant with zero manual <c>DigitalInventoryCode</c> stock
/// but a READY automated-supplier route (per <see cref="IFulfillmentRouter"/>) must no longer be forced
/// to <c>IsOutOfStock = true</c> by the storefront's product-list and PDP-configuration queries — unless
/// its <see cref="FulfillmentMode"/> says otherwise. Covers both storefront read paths
/// (<see cref="GetStorefrontProductConfigurationQueryHandler"/> for PDP,
/// <see cref="GetStorefrontProductConfigurationsQueryHandler"/> for the product list), and proves
/// checkout (<c>CartLineValidator</c>) can never contradict what these queries display.
/// </summary>
public sealed class GetStorefrontProductConfigurationSupplierAvailabilityTests
{
    private static async Task<(Product Product, ProductVariant Variant, HAMBOX.Modules.Catalog.Infrastructure.Persistence.CatalogDbContext CatalogDb)>
        SeedAsync(FulfillmentMode mode)
    {
        var (_, catalogDb) = CommerceTestDbContextFactory.Create();
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

        return (product, variant, catalogDb);
    }

    private static FakeFulfillmentRouter RouterWithReadiness(Guid variantId, FulfillmentMode mode, bool supplierReady) =>
        new FakeFulfillmentRouter().Also(r => r.SetReadiness(
            variantId,
            new FulfillmentReadiness(mode, mode is FulfillmentMode.ManualOnly or FulfillmentMode.ManualFirst,
                supplierReady ? new FulfillmentSupplierCandidate(Guid.NewGuid(), Guid.NewGuid()) : null)));

    // ===================== PDP (single-product configuration) =====================

    [Fact]
    public async Task ManualOnly_ZeroManualStock_ReadySupplierMapping_StillOutOfStock()
    {
        // (A) A supplier mapping must never rescue ManualOnly — matches CartLineValidator exactly.
        var (product, variant, catalogDb) = await SeedAsync(FulfillmentMode.ManualOnly);
        var inventory = new FakeInventoryEngine(catalogDb); // 0 stock by default
        var router = RouterWithReadiness(variant.Id, FulfillmentMode.ManualOnly, supplierReady: true);

        var dto = await HandleSingleAsync(catalogDb, inventory, router, product.Id);

        Assert.True(dto.Variants.Single().IsOutOfStock);
    }

    [Fact]
    public async Task SupplierOnly_ZeroManualStock_ReadyMapping_IsAvailable()
    {
        // (B) The exact reported scenario: SupplierOnly, 0 manual codes, a READY Bamboo mapping.
        var (product, variant, catalogDb) = await SeedAsync(FulfillmentMode.SupplierOnly);
        var inventory = new FakeInventoryEngine(catalogDb);
        var router = RouterWithReadiness(variant.Id, FulfillmentMode.SupplierOnly, supplierReady: true);

        var dto = await HandleSingleAsync(catalogDb, inventory, router, product.Id);

        Assert.False(dto.Variants.Single().IsOutOfStock);
    }

    [Fact]
    public async Task SupplierFirst_ZeroManualStock_ReadySupplier_IsAvailable()
    {
        // (C)
        var (product, variant, catalogDb) = await SeedAsync(FulfillmentMode.SupplierFirst);
        var inventory = new FakeInventoryEngine(catalogDb);
        var router = RouterWithReadiness(variant.Id, FulfillmentMode.SupplierFirst, supplierReady: true);

        var dto = await HandleSingleAsync(catalogDb, inventory, router, product.Id);

        Assert.False(dto.Variants.Single().IsOutOfStock);
    }

    [Fact]
    public async Task SupplierOnly_SupplierNotReady_IsOutOfStock()
    {
        // (D)
        var (product, variant, catalogDb) = await SeedAsync(FulfillmentMode.SupplierOnly);
        var inventory = new FakeInventoryEngine(catalogDb);
        var router = RouterWithReadiness(variant.Id, FulfillmentMode.SupplierOnly, supplierReady: false);

        var dto = await HandleSingleAsync(catalogDb, inventory, router, product.Id);

        Assert.True(dto.Variants.Single().IsOutOfStock);
    }

    [Fact]
    public async Task SupplierFirst_SupplierNotReady_ManualStockExists_StillOutOfStock()
    {
        // (E) Manual stock existing must NOT substitute for a not-ready supplier under SupplierFirst
        // at display/checkout-gating time — identical to CartLineValidator_SupplierFirst_
        // ManualStockSufficientButNoReadySupplier_StillFails in FulfillmentRoutingTests. The real
        // "fallback" for SupplierFirst is OrderFulfillmentService's post-payment terminal-failure
        // path (a fulfillment-execution concern), not a purchasability signal shown pre-checkout.
        var (product, variant, catalogDb) = await SeedAsync(FulfillmentMode.SupplierFirst);
        var inventory = new FakeInventoryEngine(catalogDb);
        inventory.AvailableStockByVariant[variant.Id] = 50;
        var router = RouterWithReadiness(variant.Id, FulfillmentMode.SupplierFirst, supplierReady: false);

        var dto = await HandleSingleAsync(catalogDb, inventory, router, product.Id);

        Assert.True(dto.Variants.Single().IsOutOfStock);
    }

    [Fact]
    public async Task ManualOnly_ExistingProducts_Unchanged_ManualStockAloneDecides()
    {
        // (K) Regression guard: ManualOnly's storefront answer must stay purely manual-driven — a
        // router that WOULD report ready (if consulted) must have zero effect.
        var (product, variant, catalogDb) = await SeedAsync(FulfillmentMode.ManualOnly);
        var inventory = new FakeInventoryEngine(catalogDb);
        inventory.AvailableStockByVariant[variant.Id] = 3;
        var router = RouterWithReadiness(variant.Id, FulfillmentMode.ManualOnly, supplierReady: true);

        var dto = await HandleSingleAsync(catalogDb, inventory, router, product.Id);

        Assert.False(dto.Variants.Single().IsOutOfStock);
        Assert.Equal(3, dto.Variants.Single().AvailableStock);
    }

    // ===================== Product list (bulk configuration) =====================

    [Fact]
    public async Task ProductList_SupplierOnly_ZeroManualStock_ReadyMapping_IsAvailable()
    {
        // (18) storefront product list must agree with PDP for the identical variant/mapping state.
        var (product, variant, catalogDb) = await SeedAsync(FulfillmentMode.SupplierOnly);
        var inventory = new FakeInventoryEngine(catalogDb);
        var router = RouterWithReadiness(variant.Id, FulfillmentMode.SupplierOnly, supplierReady: true);

        var handler = new GetStorefrontProductConfigurationsQueryHandler(catalogDb, inventory, router);
        var result = await handler.Handle(new GetStorefrontProductConfigurationsQuery([product.Id]), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var dto = Assert.Single(result.Value);
        Assert.False(dto.Variants.Single().IsOutOfStock);
    }

    // ===================== Checkout must agree with storefront display =====================

    [Fact]
    public async Task Checkout_And_Storefront_AgreeOnSupplierOnlyZeroManualStockReadySupplier()
    {
        // (L) The exact scenario the user flagged as most important: a product shown as purchasable
        // must not immediately fail at checkout because the two surfaces used contradictory rules.
        var (product, variant, catalogDb) = await SeedAsync(FulfillmentMode.SupplierOnly);
        var inventory = new FakeInventoryEngine(catalogDb);
        var router = RouterWithReadiness(variant.Id, FulfillmentMode.SupplierOnly, supplierReady: true);

        var storefrontDto = await HandleSingleAsync(catalogDb, inventory, router, product.Id);
        var storefrontSaysPurchasable = !storefrontDto.Variants.Single().IsOutOfStock;

        var cart = ShoppingCart.CreateForUser("user-1");
        cart.AddOrUpdateItem(product.Id, 1, product.Price, variant.Id);
        var validator = new CartLineValidator(inventory, router);
        var checkoutResult = await validator.ValidateAsync(
            cart,
            new Dictionary<Guid, Product> { [product.Id] = product },
            new Dictionary<Guid, ProductVariant> { [variant.Id] = variant },
            await inventory.GetVariantStockBulkAsync([variant.Id], CancellationToken.None),
            new Dictionary<Guid, HAMBOX.Application.Membership.ProductAccessInfo>(),
            HAMBOX.Application.Membership.MembershipAccessInfo.None,
            CancellationToken.None);

        Assert.True(storefrontSaysPurchasable);
        Assert.True(checkoutResult.IsSuccess);
        Assert.Equal(storefrontSaysPurchasable, checkoutResult.IsSuccess);
    }

    private static async Task<HAMBOX.Modules.Catalog.Application.Contracts.StorefrontProductConfigurationDto> HandleSingleAsync(
        HAMBOX.Modules.Catalog.Infrastructure.Persistence.CatalogDbContext catalogDb,
        FakeInventoryEngine inventory,
        FakeFulfillmentRouter router,
        Guid productId)
    {
        var handler = new GetStorefrontProductConfigurationQueryHandler(
            catalogDb, inventory, router, new FakeCurrentUserService("customer-1"),
            NullLogger<GetStorefrontProductConfigurationQueryHandler>.Instance);

        var result = await handler.Handle(new GetStorefrontProductConfigurationQuery(productId), CancellationToken.None);
        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Description : null);
        return result.Value;
    }
}

internal static class TestObjectExtensions
{
    public static T Also<T>(this T value, Action<T> action)
    {
        action(value);
        return value;
    }
}
