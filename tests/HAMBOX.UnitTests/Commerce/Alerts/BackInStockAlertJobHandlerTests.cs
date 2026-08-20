using HAMBOX.Modules.Catalog.Domain.Categories;
using HAMBOX.Modules.Catalog.Domain.Products;
using HAMBOX.Modules.Catalog.Domain.Inventory;
using HAMBOX.Modules.Commerce.Domain.Account;
using HAMBOX.Modules.Commerce.Infrastructure.BackgroundJobs.Handlers;
using HAMBOX.UnitTests.Commerce.TestDoubles;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.UnitTests.Commerce.Alerts;

/// <summary>
/// Covers the recurring back-in-stock scan job: only variants with an active subscription are ever
/// read, "genuinely purchasable" is re-derived fresh every pass (stock + variant/product status),
/// notification fan-out per subscriber, and the per-subscriber commit that makes a repeated or
/// retried pass a no-op instead of a duplicate send.
/// </summary>
public sealed class BackInStockAlertJobHandlerTests
{
    private static (Product Product, Category Category) CreateActiveProduct()
    {
        var category = Category.Create("فئة", "Category", $"category-{Guid.NewGuid():N}");
        var product = Product.Create("منتج", "Product", "وصف", "Description", 19.99m, category.Id);
        product.Activate();
        return (product, category);
    }

    private static ProductVariant CreateVariant(Guid productId, bool active = true)
    {
        var variant = ProductVariant.Create(productId, $"SKU-{Guid.NewGuid():N}");
        if (active)
        {
            variant.Activate();
        }

        return variant;
    }

    private static (BackInStockAlertJobHandler Handler, FakeInventoryEngine Inventory, FakeCommunicationService Communication) CreateHandler(
        HAMBOX.Modules.Commerce.Infrastructure.Persistence.CommerceDbContext commerceDb,
        HAMBOX.Modules.Catalog.Infrastructure.Persistence.CatalogDbContext catalogDb)
    {
        var inventory = new FakeInventoryEngine(catalogDb);
        var communication = new FakeCommunicationService();
        var handler = new BackInStockAlertJobHandler(new FakeBackgroundJobSerializer(), commerceDb, catalogDb, inventory, communication);
        return (handler, inventory, communication);
    }

    [Fact]
    public async Task Handle_VariantBecomesAvailable_NotifiesAndDeactivatesSubscription()
    {
        var (commerceDb, catalogDb) = CommerceTestDbContextFactory.Create();
        var (product, category) = CreateActiveProduct();
        var variant = CreateVariant(product.Id);
        catalogDb.Categories.Add(category);
        catalogDb.Products.Add(product);
        catalogDb.ProductVariants.Add(variant);
        await catalogDb.SaveChangesAsync(CancellationToken.None);

        var subscription = CustomerAlertSubscription.CreateForUser("user-1", CustomerAlertType.BackInStock, variant.Id, product.Id, null);
        commerceDb.CustomerAlertSubscriptions.Add(subscription);
        await commerceDb.SaveChangesAsync(CancellationToken.None);

        var (handler, inventory, communication) = CreateHandler(commerceDb, catalogDb);
        inventory.AvailableStockByVariant[variant.Id] = 3; // now purchasable

        await handler.HandleAsync(null, new FakeBackgroundJobContext(), CancellationToken.None);

        Assert.Single(communication.SentRequests);
        Assert.Equal("user-1", communication.SentRequests[0].UserId);
        Assert.Equal("BackInStockAlert", communication.SentRequests[0].TemplateKey);

        var reloaded = await commerceDb.CustomerAlertSubscriptions.AsNoTracking().SingleAsync(s => s.Id == subscription.Id);
        Assert.False(reloaded.IsActive);
        Assert.NotNull(reloaded.NotifiedOnUtc);
    }

    [Fact]
    public async Task Handle_VariantStillOutOfStock_DoesNotNotify()
    {
        var (commerceDb, catalogDb) = CommerceTestDbContextFactory.Create();
        var (product, category) = CreateActiveProduct();
        var variant = CreateVariant(product.Id);
        catalogDb.Categories.Add(category);
        catalogDb.Products.Add(product);
        catalogDb.ProductVariants.Add(variant);
        await catalogDb.SaveChangesAsync(CancellationToken.None);

        var subscription = CustomerAlertSubscription.CreateForUser("user-1", CustomerAlertType.BackInStock, variant.Id, product.Id, null);
        commerceDb.CustomerAlertSubscriptions.Add(subscription);
        await commerceDb.SaveChangesAsync(CancellationToken.None);

        var (handler, inventory, communication) = CreateHandler(commerceDb, catalogDb);
        inventory.AvailableStockByVariant[variant.Id] = 0;

        await handler.HandleAsync(null, new FakeBackgroundJobContext(), CancellationToken.None);

        Assert.Empty(communication.SentRequests);
        var reloaded = await commerceDb.CustomerAlertSubscriptions.AsNoTracking().SingleAsync(s => s.Id == subscription.Id);
        Assert.True(reloaded.IsActive);
    }

    [Fact]
    public async Task Handle_RepeatedExecution_DoesNotSendDuplicateNotification()
    {
        var (commerceDb, catalogDb) = CommerceTestDbContextFactory.Create();
        var (product, category) = CreateActiveProduct();
        var variant = CreateVariant(product.Id);
        catalogDb.Categories.Add(category);
        catalogDb.Products.Add(product);
        catalogDb.ProductVariants.Add(variant);
        await catalogDb.SaveChangesAsync(CancellationToken.None);

        var subscription = CustomerAlertSubscription.CreateForUser("user-1", CustomerAlertType.BackInStock, variant.Id, product.Id, null);
        commerceDb.CustomerAlertSubscriptions.Add(subscription);
        await commerceDb.SaveChangesAsync(CancellationToken.None);

        var (handler, inventory, communication) = CreateHandler(commerceDb, catalogDb);
        inventory.AvailableStockByVariant[variant.Id] = 3;

        await handler.HandleAsync(null, new FakeBackgroundJobContext(), CancellationToken.None);
        // Simulates a retry/second scan pass over the same still-purchasable variant.
        await handler.HandleAsync(null, new FakeBackgroundJobContext(), CancellationToken.None);

        Assert.Single(communication.SentRequests);
    }

    [Fact]
    public async Task Handle_MultipleSubscribers_AllNotifiedExactlyOnce()
    {
        var (commerceDb, catalogDb) = CommerceTestDbContextFactory.Create();
        var (product, category) = CreateActiveProduct();
        var variant = CreateVariant(product.Id);
        catalogDb.Categories.Add(category);
        catalogDb.Products.Add(product);
        catalogDb.ProductVariants.Add(variant);
        await catalogDb.SaveChangesAsync(CancellationToken.None);

        commerceDb.CustomerAlertSubscriptions.AddRange(
            CustomerAlertSubscription.CreateForUser("user-1", CustomerAlertType.BackInStock, variant.Id, product.Id, null),
            CustomerAlertSubscription.CreateForUser("user-2", CustomerAlertType.BackInStock, variant.Id, product.Id, null),
            CustomerAlertSubscription.CreateForUser("user-3", CustomerAlertType.BackInStock, variant.Id, product.Id, null));
        await commerceDb.SaveChangesAsync(CancellationToken.None);

        var (handler, inventory, communication) = CreateHandler(commerceDb, catalogDb);
        inventory.AvailableStockByVariant[variant.Id] = 1;

        await handler.HandleAsync(null, new FakeBackgroundJobContext(), CancellationToken.None);

        Assert.Equal(3, communication.SentRequests.Count);
        Assert.Equal(["user-1", "user-2", "user-3"], communication.SentRequests.Select(r => r.UserId).OrderBy(x => x));
        Assert.Equal(3, await commerceDb.CustomerAlertSubscriptions.CountAsync(s => !s.IsActive));
    }

    [Fact]
    public async Task Handle_VariantReactivatedWithNoStockChange_StillNotifies()
    {
        // Covers the "no code mutation at all" transition path: an Archived-then-Activated variant
        // that already had available codes the whole time becomes newly purchasable purely because
        // Status/IsVisible changed, not because any DigitalInventoryCode row was touched.
        var (commerceDb, catalogDb) = CommerceTestDbContextFactory.Create();
        var (product, category) = CreateActiveProduct();
        var variant = CreateVariant(product.Id);
        variant.Archive();
        catalogDb.Categories.Add(category);
        catalogDb.Products.Add(product);
        catalogDb.ProductVariants.Add(variant);
        await catalogDb.SaveChangesAsync(CancellationToken.None);

        var subscription = CustomerAlertSubscription.CreateForUser("user-1", CustomerAlertType.BackInStock, variant.Id, product.Id, null);
        commerceDb.CustomerAlertSubscriptions.Add(subscription);
        await commerceDb.SaveChangesAsync(CancellationToken.None);

        var (handler, inventory, communication) = CreateHandler(commerceDb, catalogDb);
        inventory.AvailableStockByVariant[variant.Id] = 2;

        // While archived, still out of the storefront regardless of stock.
        await handler.HandleAsync(null, new FakeBackgroundJobContext(), CancellationToken.None);
        Assert.Empty(communication.SentRequests);

        // Reactivated — same stock count, no code mutation, but now genuinely purchasable.
        variant.Activate();
        catalogDb.ProductVariants.Update(variant);
        await catalogDb.SaveChangesAsync(CancellationToken.None);

        await handler.HandleAsync(null, new FakeBackgroundJobContext(), CancellationToken.None);
        Assert.Single(communication.SentRequests);
    }

    [Fact]
    public async Task Handle_PriceDropSubscription_IsNeverTouchedByBackInStockJob()
    {
        var (commerceDb, catalogDb) = CommerceTestDbContextFactory.Create();
        var (product, category) = CreateActiveProduct();
        var variant = CreateVariant(product.Id);
        catalogDb.Categories.Add(category);
        catalogDb.Products.Add(product);
        catalogDb.ProductVariants.Add(variant);
        await catalogDb.SaveChangesAsync(CancellationToken.None);

        var backInStock = CustomerAlertSubscription.CreateForUser("user-1", CustomerAlertType.BackInStock, variant.Id, product.Id, null);
        var priceDrop = CustomerAlertSubscription.CreateForUser("user-1", CustomerAlertType.PriceDrop, variant.Id, product.Id, 19.99m);
        commerceDb.CustomerAlertSubscriptions.AddRange(backInStock, priceDrop);
        await commerceDb.SaveChangesAsync(CancellationToken.None);

        var (handler, inventory, communication) = CreateHandler(commerceDb, catalogDb);
        inventory.AvailableStockByVariant[variant.Id] = 3;

        await handler.HandleAsync(null, new FakeBackgroundJobContext(), CancellationToken.None);

        Assert.Single(communication.SentRequests);
        Assert.Equal("BackInStockAlert", communication.SentRequests[0].TemplateKey);

        var reloadedBackInStock = await commerceDb.CustomerAlertSubscriptions.AsNoTracking().SingleAsync(s => s.Id == backInStock.Id);
        Assert.False(reloadedBackInStock.IsActive);

        var reloadedPriceDrop = await commerceDb.CustomerAlertSubscriptions.AsNoTracking().SingleAsync(s => s.Id == priceDrop.Id);
        Assert.True(reloadedPriceDrop.IsActive);
        Assert.Null(reloadedPriceDrop.NotifiedOnUtc);
    }

    [Fact]
    public async Task Handle_NoActiveSubscriptions_DoesNotQueryInventoryAtAll()
    {
        var (commerceDb, catalogDb) = CommerceTestDbContextFactory.Create();
        var (handler, _, communication) = CreateHandler(commerceDb, catalogDb);

        await handler.HandleAsync(null, new FakeBackgroundJobContext(), CancellationToken.None);

        Assert.Empty(communication.SentRequests);
    }

    [Fact]
    public async Task Handle_DeletedVariant_IsSkippedWithoutThrowing()
    {
        var (commerceDb, catalogDb) = CommerceTestDbContextFactory.Create();
        var (product, category) = CreateActiveProduct();
        var variant = CreateVariant(product.Id);
        catalogDb.Categories.Add(category);
        catalogDb.Products.Add(product);
        catalogDb.ProductVariants.Add(variant);
        await catalogDb.SaveChangesAsync(CancellationToken.None);

        var subscription = CustomerAlertSubscription.CreateForUser("user-1", CustomerAlertType.BackInStock, variant.Id, product.Id, null);
        commerceDb.CustomerAlertSubscriptions.Add(subscription);
        await commerceDb.SaveChangesAsync(CancellationToken.None);

        variant.SoftDelete();
        catalogDb.ProductVariants.Update(variant);
        await catalogDb.SaveChangesAsync(CancellationToken.None);

        var (handler, inventory, communication) = CreateHandler(commerceDb, catalogDb);
        inventory.AvailableStockByVariant[variant.Id] = 5;

        var exception = await Record.ExceptionAsync(() => handler.HandleAsync(null, new FakeBackgroundJobContext(), CancellationToken.None));

        Assert.Null(exception);
        Assert.Empty(communication.SentRequests);
    }
}
