using HAMBOX.Application.Fulfillment;
using HAMBOX.Modules.Catalog.Domain.Enums;

namespace HAMBOX.UnitTests.Commerce.TestDoubles;

/// <summary>
/// Configurable stand-in for <see cref="IFulfillmentRouter"/> — defaults to exactly today's baseline
/// behavior (<see cref="FulfillmentMode.ManualOnly"/>, manual allowed, no supplier candidate) so every
/// existing test that never calls <see cref="SetReadiness"/> keeps testing manual-only behavior
/// unchanged. Tests exercising routing set a specific <see cref="FulfillmentReadiness"/> per variant id.
/// </summary>
internal sealed class FakeFulfillmentRouter : IFulfillmentRouter
{
    private readonly Dictionary<Guid, FulfillmentReadiness> _readinessByVariant = [];
    private readonly Dictionary<Guid, decimal> _effectivePriceByVariant = [];

    public FulfillmentReadiness DefaultReadiness { get; set; } = new(FulfillmentMode.ManualOnly, true, null);

    public void SetReadiness(Guid variantId, FulfillmentReadiness readiness) => _readinessByVariant[variantId] = readiness;

    /// <summary>Defaults to "no supplier-derived override" for every variant — matches today's baseline (PriceOverride ?? Product.Price) unless a test opts in.</summary>
    public void SetEffectivePrice(Guid variantId, decimal price) => _effectivePriceByVariant[variantId] = price;

    public Task<FulfillmentReadiness> GetReadinessAsync(Guid variantId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_readinessByVariant.TryGetValue(variantId, out var readiness) ? readiness : DefaultReadiness);

    public Task<IReadOnlyDictionary<Guid, FulfillmentReadiness>> GetReadinessBulkAsync(
        IEnumerable<Guid> variantIds, CancellationToken cancellationToken = default)
    {
        IReadOnlyDictionary<Guid, FulfillmentReadiness> result = variantIds.Distinct()
            .ToDictionary(id => id, id => _readinessByVariant.TryGetValue(id, out var readiness) ? readiness : DefaultReadiness);
        return Task.FromResult(result);
    }

    public Task<IReadOnlyDictionary<Guid, decimal>> GetEffectivePriceOverridesBulkAsync(
        IEnumerable<Guid> variantIds, CancellationToken cancellationToken = default)
    {
        IReadOnlyDictionary<Guid, decimal> result = variantIds.Distinct()
            .Where(_effectivePriceByVariant.ContainsKey)
            .ToDictionary(id => id, id => _effectivePriceByVariant[id]);
        return Task.FromResult(result);
    }
}
