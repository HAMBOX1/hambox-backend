using HAMBOX.Modules.Catalog.Domain.Categories;
using HAMBOX.Modules.Catalog.Domain.Products;
using HAMBOX.Modules.Commerce.Application.Errors;
using HAMBOX.Modules.Commerce.Application.Features.Cart.MergeCart;
using HAMBOX.Modules.Commerce.Application.Services;
using HAMBOX.Modules.Commerce.Domain.Carts;
using HAMBOX.UnitTests.Commerce.TestDoubles;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.UnitTests.Commerce.Checkout;

/// <summary>Audit R24: a guest's cart is folded into the account cart at login — quantities add up, nothing is lost or duplicated.</summary>
public sealed class GuestCartMergeTests
{
    private const string GuestId = "guest-session-1";

    private static async Task<(
        HAMBOX.Modules.Commerce.Infrastructure.Persistence.CommerceDbContext Commerce,
        HAMBOX.Modules.Catalog.Infrastructure.Persistence.CatalogDbContext Catalog,
        Product ProductA,
        Product ProductB)> SeedAsync()
    {
        var (commerce, catalog) = CommerceTestDbContextFactory.Create();
        var category = Category.Create("فئة", "Category", $"cat-{Guid.NewGuid():N}");
        var a = Product.Create("أ", "Product A", "d", "d", 10m, category.Id);
        var b = Product.Create("ب", "Product B", "d", "d", 20m, category.Id);
        a.Activate();
        b.Activate();
        catalog.Categories.Add(category);
        catalog.Products.AddRange(a, b);
        await catalog.SaveChangesAsync();
        return (commerce, catalog, a, b);
    }

    private static MergeCartCommandHandler CreateHandler(
        HAMBOX.Modules.Commerce.Infrastructure.Persistence.CommerceDbContext commerce,
        HAMBOX.Modules.Catalog.Infrastructure.Persistence.CatalogDbContext catalog,
        FakeCurrentUserService user)
    {
        var builder = new CartResponseBuilder(
            commerce, catalog, new FakePromotionEngine(), new FakeMembershipEngine(), user,
            new FakePaymentGatewayConfigurationProvider());
        return new MergeCartCommandHandler(commerce, catalog, user, builder);
    }

    private static ShoppingCart GuestCart(params (Guid ProductId, int Quantity)[] lines)
    {
        var cart = ShoppingCart.CreateForGuest(GuestId);
        foreach (var (productId, quantity) in lines)
        {
            cart.AddOrUpdateItem(productId, quantity, 10m);
        }

        return cart;
    }

    [Fact]
    public async Task GuestItems_MoveIntoTheAccountCart_AndTheGuestCartIsRemoved()
    {
        var (commerce, catalog, a, b) = await SeedAsync();
        commerce.ShoppingCarts.Add(GuestCart((a.Id, 2), (b.Id, 1)));
        await commerce.SaveChangesAsync();

        var result = await CreateHandler(commerce, catalog, new FakeCurrentUserService("user-1"))
            .Handle(new MergeCartCommand(GuestId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Items.Count);

        var carts = await commerce.ShoppingCarts.Include(c => c.Items).ToListAsync();
        var userCart = Assert.Single(carts);
        Assert.Equal("user-1", userCart.UserId);
        Assert.Equal(3, userCart.Items.Sum(i => i.Quantity));
        Assert.DoesNotContain(carts, c => c.GuestSessionId == GuestId);
    }

    [Fact]
    public async Task SameProductInBothCarts_HasItsQuantitiesAdded_NotDuplicated()
    {
        var (commerce, catalog, a, _) = await SeedAsync();
        var userCart = ShoppingCart.CreateForUser("user-1");
        userCart.AddOrUpdateItem(a.Id, 1, 10m);
        commerce.ShoppingCarts.AddRange(userCart, GuestCart((a.Id, 3)));
        await commerce.SaveChangesAsync();

        var result = await CreateHandler(commerce, catalog, new FakeCurrentUserService("user-1"))
            .Handle(new MergeCartCommand(GuestId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var line = Assert.Single(result.Value.Items);
        Assert.Equal(4, line.Quantity);
    }

    [Fact]
    public async Task MergingAnUnknownGuestCart_JustReturnsTheAccountCart()
    {
        var (commerce, catalog, a, _) = await SeedAsync();
        var userCart = ShoppingCart.CreateForUser("user-1");
        userCart.AddOrUpdateItem(a.Id, 2, 10m);
        commerce.ShoppingCarts.Add(userCart);
        await commerce.SaveChangesAsync();

        var result = await CreateHandler(commerce, catalog, new FakeCurrentUserService("user-1"))
            .Handle(new MergeCartCommand("no-such-guest"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, Assert.Single(result.Value.Items).Quantity);
    }

    [Fact]
    public async Task AnonymousCaller_CannotMerge()
    {
        var (commerce, catalog, _, _) = await SeedAsync();

        var result = await CreateHandler(commerce, catalog, new FakeCurrentUserService(null))
            .Handle(new MergeCartCommand(GuestId), CancellationToken.None);

        Assert.Equal(CommerceErrors.AuthenticationRequired.Code, result.Error.Code);
    }
}
