using HAMBOX.Modules.Catalog.Domain.Categories;
using HAMBOX.Modules.Catalog.Domain.Products;
using HAMBOX.Modules.Catalog.Domain.Inventory;
using HAMBOX.Modules.Commerce.Application.Errors;
using HAMBOX.Modules.Commerce.Application.Features.Account.Alerts.ClaimGuestAlertSubscriptions;
using HAMBOX.Modules.Commerce.Application.Features.Account.Alerts.CreateAlertSubscription;
using HAMBOX.Modules.Commerce.Application.Features.Account.Alerts.GetMyAlertSubscriptions;
using HAMBOX.Modules.Commerce.Application.Features.Account.Alerts.RemoveAlertSubscription;
using HAMBOX.Modules.Commerce.Domain.Account;
using HAMBOX.UnitTests.Commerce.TestDoubles;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.UnitTests.Commerce.Alerts;

/// <summary>
/// Covers the customer-facing CustomerAlertSubscription CQRS surface: create (authenticated and
/// anonymous), duplicate prevention, ownership-scoped delete/list, and the guest-to-authenticated
/// claim flow.
/// </summary>
public sealed class CustomerAlertSubscriptionHandlersTests
{
    private static (Product Product, Category Category) CreateActiveProduct(decimal price = 19.99m)
    {
        var category = Category.Create("فئة", "Category", $"category-{Guid.NewGuid():N}");
        var product = Product.Create("منتج", "Product", "وصف", "Description", price, category.Id);
        product.Activate();
        return (product, category);
    }

    private static ProductVariant CreateVariant(Guid productId, decimal? priceOverride = null, bool active = true)
    {
        var variant = ProductVariant.Create(productId, $"SKU-{Guid.NewGuid():N}", priceOverride: priceOverride);
        if (active)
        {
            variant.Activate();
        }

        return variant;
    }

    [Fact]
    public async Task Create_AuthenticatedBackInStock_OnUnavailableVariant_Succeeds()
    {
        var (commerceDb, catalogDb) = CommerceTestDbContextFactory.Create();
        var (product, category) = CreateActiveProduct();
        var variant = CreateVariant(product.Id);
        catalogDb.Categories.Add(category);
        catalogDb.Products.Add(product);
        catalogDb.ProductVariants.Add(variant);
        await catalogDb.SaveChangesAsync(CancellationToken.None);

        var inventory = new FakeInventoryEngine(catalogDb);
        inventory.AvailableStockByVariant[variant.Id] = 0;
        var handler = new CreateAlertSubscriptionCommandHandler(commerceDb, catalogDb, inventory, new FakeFulfillmentRouter(), new FakeCurrentUserService("user-1"));

        var result = await handler.Handle(
            new CreateAlertSubscriptionCommand(variant.Id, CustomerAlertType.BackInStock, GuestSessionId: null),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(product.Id, result.Value.ProductId);
        var stored = await commerceDb.CustomerAlertSubscriptions.SingleAsync();
        Assert.Equal("user-1", stored.UserId);
        Assert.True(stored.IsActive);
        Assert.Null(stored.LastObservedPrice);
    }

    [Fact]
    public async Task Create_BackInStock_OnAlreadyAvailableVariant_Fails()
    {
        var (commerceDb, catalogDb) = CommerceTestDbContextFactory.Create();
        var (product, category) = CreateActiveProduct();
        var variant = CreateVariant(product.Id);
        catalogDb.Categories.Add(category);
        catalogDb.Products.Add(product);
        catalogDb.ProductVariants.Add(variant);
        await catalogDb.SaveChangesAsync(CancellationToken.None);

        var inventory = new FakeInventoryEngine(catalogDb);
        inventory.AvailableStockByVariant[variant.Id] = 5; // already purchasable
        var handler = new CreateAlertSubscriptionCommandHandler(commerceDb, catalogDb, inventory, new FakeFulfillmentRouter(), new FakeCurrentUserService("user-1"));

        var result = await handler.Handle(
            new CreateAlertSubscriptionCommand(variant.Id, CustomerAlertType.BackInStock, GuestSessionId: null),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(CommerceErrors.VariantAlreadyAvailable.Code, result.Error.Code);
    }

    [Fact]
    public async Task Create_PriceDrop_StoresCurrentEffectivePriceAsBaseline()
    {
        var (commerceDb, catalogDb) = CommerceTestDbContextFactory.Create();
        var (product, category) = CreateActiveProduct(price: 19.99m);
        var variant = CreateVariant(product.Id, priceOverride: 12.99m);
        catalogDb.Categories.Add(category);
        catalogDb.Products.Add(product);
        catalogDb.ProductVariants.Add(variant);
        await catalogDb.SaveChangesAsync(CancellationToken.None);

        var inventory = new FakeInventoryEngine(catalogDb);
        var handler = new CreateAlertSubscriptionCommandHandler(commerceDb, catalogDb, inventory, new FakeFulfillmentRouter(), new FakeCurrentUserService("user-1"));

        var result = await handler.Handle(
            new CreateAlertSubscriptionCommand(variant.Id, CustomerAlertType.PriceDrop, GuestSessionId: null),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var stored = await commerceDb.CustomerAlertSubscriptions.SingleAsync();
        Assert.Equal(12.99m, stored.LastObservedPrice);
    }

    [Fact]
    public async Task Create_DuplicateActiveSubscription_Fails()
    {
        var (commerceDb, catalogDb) = CommerceTestDbContextFactory.Create();
        var (product, category) = CreateActiveProduct();
        var variant = CreateVariant(product.Id);
        catalogDb.Categories.Add(category);
        catalogDb.Products.Add(product);
        catalogDb.ProductVariants.Add(variant);
        await catalogDb.SaveChangesAsync(CancellationToken.None);

        var inventory = new FakeInventoryEngine(catalogDb);
        var handler = new CreateAlertSubscriptionCommandHandler(commerceDb, catalogDb, inventory, new FakeFulfillmentRouter(), new FakeCurrentUserService("user-1"));

        var first = await handler.Handle(
            new CreateAlertSubscriptionCommand(variant.Id, CustomerAlertType.PriceDrop, GuestSessionId: null), CancellationToken.None);
        var second = await handler.Handle(
            new CreateAlertSubscriptionCommand(variant.Id, CustomerAlertType.PriceDrop, GuestSessionId: null), CancellationToken.None);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsFailure);
        Assert.Equal(CommerceErrors.AlertSubscriptionExists.Code, second.Error.Code);
    }

    [Fact]
    public async Task Create_BackInStockAndPriceDrop_SameVariantSameUser_BothSucceed()
    {
        // The two alert types are independent — subscribing to both for the same variant must not
        // collide, since the unique index is scoped to (owner, variant, AlertType).
        var (commerceDb, catalogDb) = CommerceTestDbContextFactory.Create();
        var (product, category) = CreateActiveProduct();
        var variant = CreateVariant(product.Id);
        catalogDb.Categories.Add(category);
        catalogDb.Products.Add(product);
        catalogDb.ProductVariants.Add(variant);
        await catalogDb.SaveChangesAsync(CancellationToken.None);

        var inventory = new FakeInventoryEngine(catalogDb);
        inventory.AvailableStockByVariant[variant.Id] = 0;
        var handler = new CreateAlertSubscriptionCommandHandler(commerceDb, catalogDb, inventory, new FakeFulfillmentRouter(), new FakeCurrentUserService("user-1"));

        var backInStock = await handler.Handle(
            new CreateAlertSubscriptionCommand(variant.Id, CustomerAlertType.BackInStock, GuestSessionId: null), CancellationToken.None);
        var priceDrop = await handler.Handle(
            new CreateAlertSubscriptionCommand(variant.Id, CustomerAlertType.PriceDrop, GuestSessionId: null), CancellationToken.None);

        Assert.True(backInStock.IsSuccess);
        Assert.True(priceDrop.IsSuccess);
        Assert.Equal(2, await commerceDb.CustomerAlertSubscriptions.CountAsync());
    }

    [Fact]
    public async Task Create_ArchivedVariant_Fails()
    {
        var (commerceDb, catalogDb) = CommerceTestDbContextFactory.Create();
        var (product, category) = CreateActiveProduct();
        var variant = CreateVariant(product.Id);
        variant.Archive();
        catalogDb.Categories.Add(category);
        catalogDb.Products.Add(product);
        catalogDb.ProductVariants.Add(variant);
        await catalogDb.SaveChangesAsync(CancellationToken.None);

        var inventory = new FakeInventoryEngine(catalogDb);
        var handler = new CreateAlertSubscriptionCommandHandler(commerceDb, catalogDb, inventory, new FakeFulfillmentRouter(), new FakeCurrentUserService("user-1"));

        var result = await handler.Handle(
            new CreateAlertSubscriptionCommand(variant.Id, CustomerAlertType.PriceDrop, GuestSessionId: null), CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task Create_AnonymousWithoutGuestSession_Fails()
    {
        var (commerceDb, catalogDb) = CommerceTestDbContextFactory.Create();
        var (product, category) = CreateActiveProduct();
        var variant = CreateVariant(product.Id);
        catalogDb.Categories.Add(category);
        catalogDb.Products.Add(product);
        catalogDb.ProductVariants.Add(variant);
        await catalogDb.SaveChangesAsync(CancellationToken.None);

        var inventory = new FakeInventoryEngine(catalogDb);
        var handler = new CreateAlertSubscriptionCommandHandler(commerceDb, catalogDb, inventory, new FakeFulfillmentRouter(), new FakeCurrentUserService(null));

        var result = await handler.Handle(
            new CreateAlertSubscriptionCommand(variant.Id, CustomerAlertType.PriceDrop, GuestSessionId: null), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(CommerceErrors.AlertSubscriptionOwnerRequired.Code, result.Error.Code);
    }

    [Fact]
    public async Task Create_AnonymousWithGuestSession_Succeeds()
    {
        var (commerceDb, catalogDb) = CommerceTestDbContextFactory.Create();
        var (product, category) = CreateActiveProduct();
        var variant = CreateVariant(product.Id);
        catalogDb.Categories.Add(category);
        catalogDb.Products.Add(product);
        catalogDb.ProductVariants.Add(variant);
        await catalogDb.SaveChangesAsync(CancellationToken.None);

        var inventory = new FakeInventoryEngine(catalogDb);
        var handler = new CreateAlertSubscriptionCommandHandler(commerceDb, catalogDb, inventory, new FakeFulfillmentRouter(), new FakeCurrentUserService(null));

        var result = await handler.Handle(
            new CreateAlertSubscriptionCommand(variant.Id, CustomerAlertType.PriceDrop, GuestSessionId: "guest-abc"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var stored = await commerceDb.CustomerAlertSubscriptions.SingleAsync();
        Assert.Null(stored.UserId);
        Assert.Equal("guest-abc", stored.GuestSessionId);
    }

    [Fact]
    public async Task Remove_OwnSubscription_Succeeds()
    {
        var (commerceDb, catalogDb) = CommerceTestDbContextFactory.Create();
        var subscription = CustomerAlertSubscription.CreateForUser("user-1", CustomerAlertType.PriceDrop, Guid.NewGuid(), Guid.NewGuid(), 10m);
        commerceDb.CustomerAlertSubscriptions.Add(subscription);
        await commerceDb.SaveChangesAsync(CancellationToken.None);

        var handler = new RemoveAlertSubscriptionCommandHandler(commerceDb, new FakeCurrentUserService("user-1"));
        var result = await handler.Handle(new RemoveAlertSubscriptionCommand(subscription.Id, GuestSessionId: null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(await commerceDb.CustomerAlertSubscriptions.AnyAsync());
    }

    [Fact]
    public async Task Remove_AnotherCustomersSubscription_Fails()
    {
        var (commerceDb, catalogDb) = CommerceTestDbContextFactory.Create();
        var subscription = CustomerAlertSubscription.CreateForUser("user-1", CustomerAlertType.PriceDrop, Guid.NewGuid(), Guid.NewGuid(), 10m);
        commerceDb.CustomerAlertSubscriptions.Add(subscription);
        await commerceDb.SaveChangesAsync(CancellationToken.None);

        var handler = new RemoveAlertSubscriptionCommandHandler(commerceDb, new FakeCurrentUserService("user-2"));
        var result = await handler.Handle(new RemoveAlertSubscriptionCommand(subscription.Id, GuestSessionId: null), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(CommerceErrors.AlertSubscriptionNotFound.Code, result.Error.Code);
        Assert.True(await commerceDb.CustomerAlertSubscriptions.AnyAsync(s => s.Id == subscription.Id));
    }

    [Fact]
    public async Task GetMyAlertSubscriptions_OnlyReturnsCallersOwnRows()
    {
        var (commerceDb, catalogDb) = CommerceTestDbContextFactory.Create();
        var mine = CustomerAlertSubscription.CreateForUser("user-1", CustomerAlertType.PriceDrop, Guid.NewGuid(), Guid.NewGuid(), 10m);
        var theirs = CustomerAlertSubscription.CreateForUser("user-2", CustomerAlertType.PriceDrop, Guid.NewGuid(), Guid.NewGuid(), 10m);
        commerceDb.CustomerAlertSubscriptions.AddRange(mine, theirs);
        await commerceDb.SaveChangesAsync(CancellationToken.None);

        var handler = new GetMyAlertSubscriptionsQueryHandler(commerceDb, catalogDb, new FakeCurrentUserService("user-1"));
        var result = await handler.Handle(new GetMyAlertSubscriptionsQuery(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value);
        Assert.Equal(mine.Id, result.Value[0].Id);
    }

    [Fact]
    public async Task Claim_ReassignsGuestSubscriptionsToAuthenticatedUser()
    {
        var (commerceDb, catalogDb) = CommerceTestDbContextFactory.Create();
        var guestSubscription = CustomerAlertSubscription.CreateForGuest("guest-abc", CustomerAlertType.PriceDrop, Guid.NewGuid(), Guid.NewGuid(), 10m);
        commerceDb.CustomerAlertSubscriptions.Add(guestSubscription);
        await commerceDb.SaveChangesAsync(CancellationToken.None);

        var handler = new ClaimGuestAlertSubscriptionsCommandHandler(commerceDb, new FakeCurrentUserService("user-1"));
        var result = await handler.Handle(new ClaimGuestAlertSubscriptionsCommand("guest-abc"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value);
        var stored = await commerceDb.CustomerAlertSubscriptions.SingleAsync();
        Assert.Equal("user-1", stored.UserId);
        Assert.Null(stored.GuestSessionId);
    }

    [Fact]
    public async Task Claim_WhenUserAlreadyHasSameSubscription_DropsRedundantGuestRowWithoutConflict()
    {
        var (commerceDb, catalogDb) = CommerceTestDbContextFactory.Create();
        var variantId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var existing = CustomerAlertSubscription.CreateForUser("user-1", CustomerAlertType.PriceDrop, variantId, productId, 10m);
        var guestDuplicate = CustomerAlertSubscription.CreateForGuest("guest-abc", CustomerAlertType.PriceDrop, variantId, productId, 8m);
        commerceDb.CustomerAlertSubscriptions.AddRange(existing, guestDuplicate);
        await commerceDb.SaveChangesAsync(CancellationToken.None);

        var handler = new ClaimGuestAlertSubscriptionsCommandHandler(commerceDb, new FakeCurrentUserService("user-1"));
        var result = await handler.Handle(new ClaimGuestAlertSubscriptionsCommand("guest-abc"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value);
        var remaining = await commerceDb.CustomerAlertSubscriptions.ToListAsync();
        Assert.Single(remaining);
        Assert.Equal(existing.Id, remaining[0].Id);
    }

    [Fact]
    public async Task Claim_WithNoGuestSessionId_IsHarmlessNoOp()
    {
        var (commerceDb, catalogDb) = CommerceTestDbContextFactory.Create();
        var handler = new ClaimGuestAlertSubscriptionsCommandHandler(commerceDb, new FakeCurrentUserService("user-1"));

        var result = await handler.Handle(new ClaimGuestAlertSubscriptionsCommand(null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value);
    }
}
