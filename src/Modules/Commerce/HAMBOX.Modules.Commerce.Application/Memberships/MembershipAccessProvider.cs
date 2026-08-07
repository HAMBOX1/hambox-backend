using HAMBOX.Application.Membership;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Commerce.Application.Memberships;

/// <summary>
/// Implements the BuildingBlocks <see cref="IMembershipAccessProvider"/> contract so other
/// modules (Catalog, Themes, Support, ...) can enforce membership benefits without depending
/// on Commerce directly — mirrors how ICommunicationService/IPlatformSettingsProvider work.
/// </summary>
internal sealed class MembershipAccessProvider(IMembershipEngine engine, ICommerceDbContext dbContext)
    : IMembershipAccessProvider
{
    public async Task<MembershipAccessInfo> GetAccessInfoAsync(string? userId, CancellationToken cancellationToken = default)
    {
        var snapshot = await engine.ResolveAsync(userId, cancellationToken);
        if (!snapshot.HasActiveMembership)
        {
            return MembershipAccessInfo.None;
        }

        var activeFlags = snapshot.FeatureFlags
            .Where(kv => kv.Value.Equals("true", StringComparison.OrdinalIgnoreCase))
            .Select(kv => kv.Key)
            .ToList();

        // A default-plan fallback (no explicit subscription row) carries SubscriptionId = Guid.Empty
        // and a synthetic 10-years-out ExpiresOnUtc — neither is real, so don't surface them.
        var hasRealSubscription = snapshot.SubscriptionId != Guid.Empty;

        return new MembershipAccessInfo(
            snapshot.PlanId,
            snapshot.PlanName,
            snapshot.PlanSlug,
            true,
            activeFlags,
            snapshot.MaxPurchasesPerMonth,
            snapshot.EarlyAccessDays,
            snapshot.HasPrioritySupport,
            hasRealSubscription ? snapshot.SubscriptionId : null,
            hasRealSubscription ? snapshot.ExpiresOnUtc : null);
    }

    public async Task<ProductAccessInfo> GetProductAccessAsync(string? userId, Guid productId, CancellationToken cancellationToken = default)
    {
        var result = await GetProductsAccessAsync(userId, [productId], cancellationToken);
        return result.TryGetValue(productId, out var info) ? info : ProductAccessInfo.Unrestricted;
    }

    public async Task<IReadOnlyDictionary<Guid, ProductAccessInfo>> GetProductsAccessAsync(
        string? userId,
        IReadOnlyCollection<Guid> productIds,
        CancellationToken cancellationToken = default)
    {
        if (productIds.Count == 0)
        {
            return new Dictionary<Guid, ProductAccessInfo>();
        }

        var restrictions = await dbContext.MembershipPlanProductAccess.AsNoTracking()
            .Where(a => productIds.Contains(a.ProductId))
            .ToListAsync(cancellationToken);

        if (restrictions.Count == 0)
        {
            return productIds.ToDictionary(id => id, _ => ProductAccessInfo.Unrestricted);
        }

        var restrictedPlanIds = restrictions.Select(r => r.PlanId).Distinct().ToList();
        var planNames = await dbContext.MembershipPlans.AsNoTracking()
            .Where(p => restrictedPlanIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.Name, cancellationToken);

        var access = await GetAccessInfoAsync(userId, cancellationToken);
        var byProduct = restrictions.GroupBy(r => r.ProductId).ToDictionary(g => g.Key, g => g.ToList());

        var result = new Dictionary<Guid, ProductAccessInfo>();
        foreach (var productId in productIds)
        {
            if (!byProduct.TryGetValue(productId, out var rows))
            {
                result[productId] = ProductAccessInfo.Unrestricted;
                continue;
            }

            var requiredPlanIds = rows.Select(r => r.PlanId).ToList();
            var hasAccess = access.HasActiveMembership && requiredPlanIds.Contains(access.PlanId);
            var requiredNames = requiredPlanIds
                .Select(id => planNames.GetValueOrDefault(id, "Members"))
                .Distinct()
                .ToList();

            result[productId] = new ProductAccessInfo(true, hasAccess, requiredNames);
        }

        return result;
    }
}
