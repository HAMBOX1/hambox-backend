using HAMBOX.Application.Membership;
using HAMBOX.Modules.Catalog.Domain.Products;
using HAMBOX.Modules.Commerce.Application.Services;
using HAMBOX.Modules.Commerce.Domain.Carts;
using HAMBOX.UnitTests.Commerce.TestDoubles;

namespace HAMBOX.UnitTests.Commerce.Checkout;

/// <summary>A product can exempt customers with an active membership (Elite) from its purchase caps.</summary>
public sealed class PurchaseLimitsMemberExemptionTests
{
    private sealed class FakeMembership(bool subscribed) : IMembershipAccessProvider
    {
        public Task<MembershipAccessInfo> GetAccessInfoAsync(string? userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(new MembershipAccessInfo(
                Guid.NewGuid(), "Elite", "elite", subscribed, [], null, 0, false,
                subscribed ? Guid.NewGuid() : null, subscribed ? DateTime.UtcNow.AddDays(10) : null));

        public Task<ProductAccessInfo> GetProductAccessAsync(string? userId, Guid productId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyDictionary<Guid, ProductAccessInfo>> GetProductsAccessAsync(
            string? userId, IReadOnlyCollection<Guid> productIds, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private static Product Capped(bool exempt)
    {
        var product = Product.Create("منتج", "Product", "d", "d", 10m, Guid.NewGuid());
        product.SetPurchaseControls(2, null, false, exempt);
        return product;
    }

    private static CartItem Line(Guid productId, int quantity)
    {
        var cart = ShoppingCart.CreateForUser("cart-user");
        cart.AddOrUpdateItem(productId, quantity, 10m);
        return cart.Items.Single();
    }

    private static async Task<string?> ErrorCodeAsync(Product product, bool subscribed)
    {
        var (db, _) = CommerceTestDbContextFactory.Create();
        var service = new PurchaseControlService(db, null, new FakeMembership(subscribed));
        var result = await service.CheckLimitsAsync(
            "user-1", [Line(product.Id, 5)], new Dictionary<Guid, Product> { [product.Id] = product }, CancellationToken.None);
        return result.IsSuccess ? null : result.Error.Code;
    }

    [Fact]
    public async Task AMemberIsNotHeldToTheCap_WhenTheProductExemptsMembers()
    {
        Assert.Null(await ErrorCodeAsync(Capped(exempt: true), subscribed: true));
    }

    [Fact]
    public async Task ARegularCustomerIsStillHeldToTheCap_EvenWhenTheProductExemptsMembers()
    {
        Assert.Equal("Checkout.PurchaseLimitPerOrderExceeded", await ErrorCodeAsync(Capped(exempt: true), subscribed: false));
    }

    [Fact]
    public async Task AMemberIsHeldToTheCap_WhenTheProductDoesNotExemptMembers()
    {
        Assert.Equal("Checkout.PurchaseLimitPerOrderExceeded", await ErrorCodeAsync(Capped(exempt: false), subscribed: true));
    }
}
