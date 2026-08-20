using HAMBOX.Modules.Catalog.Domain.Categories;
using HAMBOX.Modules.Catalog.Domain.Products;
using HAMBOX.Modules.Catalog.Domain.Inventory;
using HAMBOX.Modules.Commerce.Domain.Account;
using HAMBOX.Modules.Commerce.Infrastructure.BackgroundJobs.Handlers;
using HAMBOX.UnitTests.Commerce.TestDoubles;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.UnitTests.Commerce.Alerts;

/// <summary>
/// Covers the recurring price-drop scan job. Only ever compares
/// <c>Variant.PriceOverride ?? Product.Price</c> against the subscription's stored baseline —
/// deliberately never a promotion/coupon/membership/currency-adjusted price.
/// </summary>
public sealed class PriceDropAlertJobHandlerTests
{
    private static (Product Product, Category Category) CreateActiveProduct(decimal price)
    {
        var category = Category.Create("فئة", "Category", $"category-{Guid.NewGuid():N}");
        var product = Product.Create("منتج", "Product", "وصف", "Description", price, category.Id);
        product.Activate();
        return (product, category);
    }

    private static ProductVariant CreateVariant(Guid productId, decimal? priceOverride)
    {
        var variant = ProductVariant.Create(productId, $"SKU-{Guid.NewGuid():N}", priceOverride: priceOverride);
        variant.Activate();
        return variant;
    }

    private static (PriceDropAlertJobHandler Handler, FakeCommunicationService Communication) CreateHandler(
        HAMBOX.Modules.Commerce.Infrastructure.Persistence.CommerceDbContext commerceDb,
        HAMBOX.Modules.Catalog.Infrastructure.Persistence.CatalogDbContext catalogDb)
    {
        var communication = new FakeCommunicationService();
        var handler = new PriceDropAlertJobHandler(new FakeBackgroundJobSerializer(), commerceDb, catalogDb, communication);
        return (handler, communication);
    }

    private static async Task<(HAMBOX.Modules.Commerce.Infrastructure.Persistence.CommerceDbContext CommerceDb,
        HAMBOX.Modules.Catalog.Infrastructure.Persistence.CatalogDbContext CatalogDb, Product Product, ProductVariant Variant)>
        SeedProductAndVariant(decimal productPrice, decimal? variantPriceOverride)
    {
        var (commerceDb, catalogDb) = CommerceTestDbContextFactory.Create();
        var (product, category) = CreateActiveProduct(productPrice);
        var variant = CreateVariant(product.Id, variantPriceOverride);
        catalogDb.Categories.Add(category);
        catalogDb.Products.Add(product);
        catalogDb.ProductVariants.Add(variant);
        await catalogDb.SaveChangesAsync(CancellationToken.None);
        return (commerceDb, catalogDb, product, variant);
    }

    [Fact]
    public async Task Handle_GenuinePriceDecrease_NotifiesAndDeactivates()
    {
        var (commerceDb, catalogDb, product, variant) = await SeedProductAndVariant(19.99m, 12.99m);
        var subscription = CustomerAlertSubscription.CreateForUser("user-1", CustomerAlertType.PriceDrop, variant.Id, product.Id, 12.99m);
        commerceDb.CustomerAlertSubscriptions.Add(subscription);
        await commerceDb.SaveChangesAsync(CancellationToken.None);

        var (handler, communication) = CreateHandler(commerceDb, catalogDb);

        // Price drops after subscribing.
        variant.Update(variant.Sku, variant.PlanId, 10.99m, variant.ComparePrice, variant.SortOrder, variant.Status, variant.IsVisible, variant.MembershipPlanId, variant.LowStockThreshold);
        catalogDb.ProductVariants.Update(variant);
        await catalogDb.SaveChangesAsync(CancellationToken.None);

        await handler.HandleAsync(null, new FakeBackgroundJobContext(), CancellationToken.None);

        Assert.Single(communication.SentRequests);
        Assert.Equal("PriceDropAlert", communication.SentRequests[0].TemplateKey);
        Assert.Equal("10.99", communication.SentRequests[0].Variables["NewPrice"]);
        Assert.Equal("12.99", communication.SentRequests[0].Variables["OldPrice"]);

        var reloaded = await commerceDb.CustomerAlertSubscriptions.AsNoTracking().SingleAsync(s => s.Id == subscription.Id);
        Assert.False(reloaded.IsActive);
        Assert.NotNull(reloaded.NotifiedOnUtc);
    }

    [Fact]
    public async Task Handle_UnchangedPrice_DoesNotNotify()
    {
        var (commerceDb, catalogDb, product, variant) = await SeedProductAndVariant(19.99m, 12.99m);
        var subscription = CustomerAlertSubscription.CreateForUser("user-1", CustomerAlertType.PriceDrop, variant.Id, product.Id, 12.99m);
        commerceDb.CustomerAlertSubscriptions.Add(subscription);
        await commerceDb.SaveChangesAsync(CancellationToken.None);

        var (handler, communication) = CreateHandler(commerceDb, catalogDb);
        await handler.HandleAsync(null, new FakeBackgroundJobContext(), CancellationToken.None);

        Assert.Empty(communication.SentRequests);
        Assert.True((await commerceDb.CustomerAlertSubscriptions.AsNoTracking().SingleAsync()).IsActive);
    }

    [Fact]
    public async Task Handle_PriceIncrease_DoesNotNotify()
    {
        var (commerceDb, catalogDb, product, variant) = await SeedProductAndVariant(19.99m, 10.99m);
        var subscription = CustomerAlertSubscription.CreateForUser("user-1", CustomerAlertType.PriceDrop, variant.Id, product.Id, 10.99m);
        commerceDb.CustomerAlertSubscriptions.Add(subscription);
        await commerceDb.SaveChangesAsync(CancellationToken.None);

        var (handler, communication) = CreateHandler(commerceDb, catalogDb);

        variant.Update(variant.Sku, variant.PlanId, 11.99m, variant.ComparePrice, variant.SortOrder, variant.Status, variant.IsVisible, variant.MembershipPlanId, variant.LowStockThreshold);
        catalogDb.ProductVariants.Update(variant);
        await catalogDb.SaveChangesAsync(CancellationToken.None);

        await handler.HandleAsync(null, new FakeBackgroundJobContext(), CancellationToken.None);

        Assert.Empty(communication.SentRequests);
    }

    [Fact]
    public async Task Handle_RepeatedExecution_DoesNotSendDuplicateNotification()
    {
        var (commerceDb, catalogDb, product, variant) = await SeedProductAndVariant(19.99m, 12.99m);
        var subscription = CustomerAlertSubscription.CreateForUser("user-1", CustomerAlertType.PriceDrop, variant.Id, product.Id, 12.99m);
        commerceDb.CustomerAlertSubscriptions.Add(subscription);
        await commerceDb.SaveChangesAsync(CancellationToken.None);

        var (handler, communication) = CreateHandler(commerceDb, catalogDb);

        variant.Update(variant.Sku, variant.PlanId, 9.99m, variant.ComparePrice, variant.SortOrder, variant.Status, variant.IsVisible, variant.MembershipPlanId, variant.LowStockThreshold);
        catalogDb.ProductVariants.Update(variant);
        await catalogDb.SaveChangesAsync(CancellationToken.None);

        await handler.HandleAsync(null, new FakeBackgroundJobContext(), CancellationToken.None);
        // Repeated/recalculated pass over the same already-notified subscription.
        await handler.HandleAsync(null, new FakeBackgroundJobContext(), CancellationToken.None);

        Assert.Single(communication.SentRequests);
    }

    [Fact]
    public async Task Handle_MultipleSubscribers_EachNotifiedIndependently()
    {
        var (commerceDb, catalogDb, product, variant) = await SeedProductAndVariant(19.99m, 12.99m);
        commerceDb.CustomerAlertSubscriptions.AddRange(
            CustomerAlertSubscription.CreateForUser("user-1", CustomerAlertType.PriceDrop, variant.Id, product.Id, 12.99m),
            CustomerAlertSubscription.CreateForUser("user-2", CustomerAlertType.PriceDrop, variant.Id, product.Id, 12.99m));
        await commerceDb.SaveChangesAsync(CancellationToken.None);

        var (handler, communication) = CreateHandler(commerceDb, catalogDb);

        variant.Update(variant.Sku, variant.PlanId, 9.99m, variant.ComparePrice, variant.SortOrder, variant.Status, variant.IsVisible, variant.MembershipPlanId, variant.LowStockThreshold);
        catalogDb.ProductVariants.Update(variant);
        await catalogDb.SaveChangesAsync(CancellationToken.None);

        await handler.HandleAsync(null, new FakeBackgroundJobContext(), CancellationToken.None);

        Assert.Equal(2, communication.SentRequests.Count);
    }

    [Fact]
    public async Task Handle_VariantANeverTriggersVariantB()
    {
        var (commerceDb, catalogDb) = CommerceTestDbContextFactory.Create();
        var (product, category) = CreateActiveProduct(19.99m);
        var variantA = CreateVariant(product.Id, 12.99m);
        var variantB = CreateVariant(product.Id, 12.99m);
        catalogDb.Categories.Add(category);
        catalogDb.Products.Add(product);
        catalogDb.ProductVariants.AddRange(variantA, variantB);
        await catalogDb.SaveChangesAsync(CancellationToken.None);

        commerceDb.CustomerAlertSubscriptions.AddRange(
            CustomerAlertSubscription.CreateForUser("user-1", CustomerAlertType.PriceDrop, variantA.Id, product.Id, 12.99m),
            CustomerAlertSubscription.CreateForUser("user-2", CustomerAlertType.PriceDrop, variantB.Id, product.Id, 12.99m));
        await commerceDb.SaveChangesAsync(CancellationToken.None);

        var (handler, communication) = CreateHandler(commerceDb, catalogDb);

        // Only variant A's price drops.
        variantA.Update(variantA.Sku, variantA.PlanId, 9.99m, variantA.ComparePrice, variantA.SortOrder, variantA.Status, variantA.IsVisible, variantA.MembershipPlanId, variantA.LowStockThreshold);
        catalogDb.ProductVariants.Update(variantA);
        await catalogDb.SaveChangesAsync(CancellationToken.None);

        await handler.HandleAsync(null, new FakeBackgroundJobContext(), CancellationToken.None);

        Assert.Single(communication.SentRequests);
        Assert.Equal("user-1", communication.SentRequests[0].UserId);
        var subscriptionB = await commerceDb.CustomerAlertSubscriptions.AsNoTracking().SingleAsync(s => s.UserId == "user-2");
        Assert.True(subscriptionB.IsActive);
    }

    [Fact]
    public async Task Handle_BackInStockSubscription_IsNeverTouchedByPriceDropJob()
    {
        var (commerceDb, catalogDb, product, variant) = await SeedProductAndVariant(19.99m, 12.99m);
        var backInStock = CustomerAlertSubscription.CreateForUser("user-1", CustomerAlertType.BackInStock, variant.Id, product.Id, null);
        var priceDrop = CustomerAlertSubscription.CreateForUser("user-1", CustomerAlertType.PriceDrop, variant.Id, product.Id, 12.99m);
        commerceDb.CustomerAlertSubscriptions.AddRange(backInStock, priceDrop);
        await commerceDb.SaveChangesAsync(CancellationToken.None);

        var (handler, communication) = CreateHandler(commerceDb, catalogDb);

        variant.Update(variant.Sku, variant.PlanId, 9.99m, variant.ComparePrice, variant.SortOrder, variant.Status, variant.IsVisible, variant.MembershipPlanId, variant.LowStockThreshold);
        catalogDb.ProductVariants.Update(variant);
        await catalogDb.SaveChangesAsync(CancellationToken.None);

        await handler.HandleAsync(null, new FakeBackgroundJobContext(), CancellationToken.None);

        Assert.Single(communication.SentRequests);
        Assert.Equal("PriceDropAlert", communication.SentRequests[0].TemplateKey);

        var reloadedBackInStock = await commerceDb.CustomerAlertSubscriptions.AsNoTracking().SingleAsync(s => s.Id == backInStock.Id);
        Assert.True(reloadedBackInStock.IsActive);
        Assert.Null(reloadedBackInStock.NotifiedOnUtc);

        var reloadedPriceDrop = await commerceDb.CustomerAlertSubscriptions.AsNoTracking().SingleAsync(s => s.Id == priceDrop.Id);
        Assert.False(reloadedPriceDrop.IsActive);
    }
}
