using HAMBOX.Application.Variants;

namespace HAMBOX.UnitTests.Catalog.Inventory.VariantLifecycle;

/// <summary>
/// In-memory stand-in for the BuildingBlocks <see cref="ICommerceVariantUsageProvider"/> contract —
/// lets Catalog-only tests configure Commerce-side counts (cart items, order items, license keys)
/// per variant without needing a real Commerce db context. <see cref="RemoveCartItemsAsync"/>
/// actually mutates its own state, so cleanup-idempotency tests can verify a second call finds
/// nothing left.
/// </summary>
internal sealed class FakeCommerceVariantUsageProvider : ICommerceVariantUsageProvider
{
    public Dictionary<Guid, int> CartItemCountByVariant { get; } = [];
    public Dictionary<Guid, int> OrderItemCountByVariant { get; } = [];
    public Dictionary<Guid, int> OrderLicenseKeyCountByVariant { get; } = [];

    public Task<CommerceVariantUsageSnapshot> GetUsageAsync(Guid variantId, CancellationToken cancellationToken = default) =>
        Task.FromResult(new CommerceVariantUsageSnapshot(
            CartItemCountByVariant.GetValueOrDefault(variantId),
            OrderItemCountByVariant.GetValueOrDefault(variantId),
            OrderLicenseKeyCountByVariant.GetValueOrDefault(variantId)));

    public Task<int> RemoveCartItemsAsync(Guid variantId, CancellationToken cancellationToken = default)
    {
        var removed = CartItemCountByVariant.GetValueOrDefault(variantId);
        CartItemCountByVariant[variantId] = 0;
        return Task.FromResult(removed);
    }
}
