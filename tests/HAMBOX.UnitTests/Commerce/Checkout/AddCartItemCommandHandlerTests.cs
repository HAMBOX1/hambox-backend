using HAMBOX.Modules.Catalog.Application.Errors;
using HAMBOX.Modules.Catalog.Domain.Categories;
using HAMBOX.Modules.Catalog.Domain.Inventory;
using HAMBOX.Modules.Catalog.Domain.Products;
using HAMBOX.Modules.Commerce.Application.Errors;
using HAMBOX.Modules.Commerce.Application.Features.Cart.AddCartItem;
using HAMBOX.Modules.Commerce.Application.Services;
using HAMBOX.UnitTests.Commerce.TestDoubles;

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
        FakeCurrentUserService currentUser)
    {
        var cartResponseBuilder = new CartResponseBuilder(
            commerceDb, catalogDb, new FakePromotionEngine(), new FakeMembershipEngine(), currentUser);

        return new AddCartItemCommandHandler(
            commerceDb, catalogDb, currentUser, inventoryEngine, cartResponseBuilder, new FakeMembershipAccessProvider());
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
}
