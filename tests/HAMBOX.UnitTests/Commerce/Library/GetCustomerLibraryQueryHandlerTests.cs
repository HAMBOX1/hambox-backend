using HAMBOX.Modules.Catalog.Domain.Categories;
using HAMBOX.Modules.Catalog.Domain.Inventory;
using HAMBOX.Modules.Catalog.Domain.Products;
using HAMBOX.Modules.Commerce.Application.Features.Account.Library;
using HAMBOX.Modules.Commerce.Domain.Account;
using HAMBOX.Modules.Commerce.Domain.Orders;
using HAMBOX.UnitTests.Commerce.TestDoubles;

namespace HAMBOX.UnitTests.Commerce.Library;

/// <summary>
/// Covers the IgnoreQueryFilters() fix: a customer's digital library must keep showing
/// platform/edition/region for a past purchase even after the variant that was bought is
/// archived or permanently deleted — the purchase itself never depended on the variant still
/// being live, only the display enrichment did.
/// </summary>
public sealed class GetCustomerLibraryQueryHandlerTests
{
    private static (Product Product, Category Category, ProductVariant Variant, ProductOptionGroup PlatformGroup, ProductOption SteamOption)
        CreateProductWithSteamVariant()
    {
        var category = Category.Create("فئة", "Category", $"category-{Guid.NewGuid():N}");
        var product = Product.Create("منتج", "Product", "وصف", "Description", 19.99m, category.Id);
        product.Activate();

        var platformGroup = ProductOptionGroup.Create(product.Id, "platform", "Platform", sortOrder: 0);
        var steamOption = platformGroup.AddOption("steam", "Steam", 0);

        var variant = ProductVariant.Create(product.Id, $"SKU-{Guid.NewGuid():N}");
        variant.SetOptions([steamOption.Id]);
        variant.Activate();

        return (product, category, variant, platformGroup, steamOption);
    }

    private static Order CreateCompletedOrderFor(Product product, ProductVariant variant, out Guid orderItemId)
    {
        var order = Order.Create(
            userId: "user-1",
            orderNumber: $"ORD-{Guid.NewGuid():N}",
            email: "buyer@example.com",
            country: "US",
            paymentMethod: "development",
            subtotal: 19.99m,
            discountAmount: 0m,
            taxAmount: 0m,
            totalAmount: 19.99m,
            items: [(product.Id, "Test Product", 1, 19.99m, variant.Id, variant.Sku)]);

        order.RecordPayment("development", $"txn-{Guid.NewGuid():N}");
        order.Complete();
        orderItemId = order.Items.Single().Id;
        return order;
    }

    [Fact]
    public async Task Handle_LiveVariant_ResolvesPlatformFromSelectedOptions()
    {
        var (commerceDb, catalogDb) = CommerceTestDbContextFactory.Create();
        var (product, category, variant, platformGroup, _) = CreateProductWithSteamVariant();

        catalogDb.Categories.Add(category);
        catalogDb.Products.Add(product);
        catalogDb.ProductOptionGroups.Add(platformGroup);
        catalogDb.ProductVariants.Add(variant);
        await catalogDb.SaveChangesAsync(CancellationToken.None);

        var order = CreateCompletedOrderFor(product, variant, out var orderItemId);
        commerceDb.Orders.Add(order);
        commerceDb.OrderLicenseKeys.Add(
            OrderLicenseKey.Create(order.Id, orderItemId, product.Id, "REAL-KEY-0001", variant.Id));
        await commerceDb.SaveChangesAsync(CancellationToken.None);

        var handler = new GetCustomerLibraryQueryHandler(commerceDb, catalogDb, new FakeCurrentUserService("user-1"));
        var result = await handler.Handle(
            new GetCustomerLibraryQuery(1, 20, null, null, null, null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var item = Assert.Single(result.Value.Items);
        Assert.Equal("Steam", item.Platform);
        Assert.Equal("Delivered", item.DeliveryStatus);
    }

    /// <summary>
    /// The actual fix under test: archiving the variant (Status=Archived, IsVisible=false) does
    /// NOT set IsDeleted, but the point still stands even for the harder case — a fully
    /// soft-deleted (IsDeleted=true) variant, which the global query filter excludes from any
    /// lookup that doesn't call IgnoreQueryFilters(). Before the fix, this returned null Platform.
    /// </summary>
    [Fact]
    public async Task Handle_PermanentlyDeletedVariant_StillResolvesPlatform_ViaIgnoreQueryFilters()
    {
        var (commerceDb, catalogDb) = CommerceTestDbContextFactory.Create();
        var (product, category, variant, platformGroup, _) = CreateProductWithSteamVariant();

        catalogDb.Categories.Add(category);
        catalogDb.Products.Add(product);
        catalogDb.ProductOptionGroups.Add(platformGroup);
        catalogDb.ProductVariants.Add(variant);
        await catalogDb.SaveChangesAsync(CancellationToken.None);

        var order = CreateCompletedOrderFor(product, variant, out var orderItemId);
        commerceDb.Orders.Add(order);
        commerceDb.OrderLicenseKeys.Add(
            OrderLicenseKey.Create(order.Id, orderItemId, product.Id, "REAL-KEY-0002", variant.Id));
        await commerceDb.SaveChangesAsync(CancellationToken.None);

        // Simulate the variant having since been permanently deleted (tombstoned).
        variant.SoftDelete();
        await catalogDb.SaveChangesAsync(CancellationToken.None);

        var handler = new GetCustomerLibraryQueryHandler(commerceDb, catalogDb, new FakeCurrentUserService("user-1"));
        var result = await handler.Handle(
            new GetCustomerLibraryQuery(1, 20, null, null, null, null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var item = Assert.Single(result.Value.Items);
        Assert.Equal("Steam", item.Platform);
        Assert.True(item.CanReveal);
        Assert.Equal("Delivered", item.DeliveryStatus);
    }

    [Fact]
    public async Task Handle_ProductAlsoDeleted_FallsBackToUnknownProduct_NeverThrows()
    {
        var (commerceDb, catalogDb) = CommerceTestDbContextFactory.Create();
        var (product, category, variant, platformGroup, _) = CreateProductWithSteamVariant();

        catalogDb.Categories.Add(category);
        catalogDb.Products.Add(product);
        catalogDb.ProductOptionGroups.Add(platformGroup);
        catalogDb.ProductVariants.Add(variant);
        await catalogDb.SaveChangesAsync(CancellationToken.None);

        var order = CreateCompletedOrderFor(product, variant, out var orderItemId);
        commerceDb.Orders.Add(order);
        commerceDb.OrderLicenseKeys.Add(
            OrderLicenseKey.Create(order.Id, orderItemId, product.Id, "REAL-KEY-0003", variant.Id));
        await commerceDb.SaveChangesAsync(CancellationToken.None);

        catalogDb.ProductVariants.Remove(variant);
        catalogDb.Products.Remove(product);
        await catalogDb.SaveChangesAsync(CancellationToken.None);

        var handler = new GetCustomerLibraryQueryHandler(commerceDb, catalogDb, new FakeCurrentUserService("user-1"));
        var result = await handler.Handle(
            new GetCustomerLibraryQuery(1, 20, null, null, null, null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var item = Assert.Single(result.Value.Items);
        Assert.Equal("Unknown Product", item.ProductNameEn);
        Assert.True(item.CanReveal);
    }
}
