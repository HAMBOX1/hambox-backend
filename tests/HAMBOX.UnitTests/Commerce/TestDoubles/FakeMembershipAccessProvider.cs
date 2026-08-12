using HAMBOX.Application.Membership;

namespace HAMBOX.UnitTests.Commerce.TestDoubles;

/// <summary>Always reports unrestricted, no-early-access-boost membership — H1's tests are about
/// variant/inventory branching, not membership gating (already covered separately).</summary>
internal sealed class FakeMembershipAccessProvider : IMembershipAccessProvider
{
    public Task<MembershipAccessInfo> GetAccessInfoAsync(string? userId, CancellationToken cancellationToken = default) =>
        Task.FromResult(MembershipAccessInfo.None);

    public Task<ProductAccessInfo> GetProductAccessAsync(string? userId, Guid productId, CancellationToken cancellationToken = default) =>
        Task.FromResult(ProductAccessInfo.Unrestricted);

    public Task<IReadOnlyDictionary<Guid, ProductAccessInfo>> GetProductsAccessAsync(
        string? userId, IReadOnlyCollection<Guid> productIds, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyDictionary<Guid, ProductAccessInfo>>(
            productIds.ToDictionary(id => id, _ => ProductAccessInfo.Unrestricted));
}
