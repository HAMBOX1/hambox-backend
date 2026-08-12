using HAMBOX.Application.Variants;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Commerce.Application.Variants;

/// <summary>
/// Implements the BuildingBlocks <see cref="ICommerceVariantUsageProvider"/> contract so Catalog's
/// variant usage-inspection/deletion flow can read Commerce-side reference counts without Catalog
/// depending on Commerce directly — mirrors how <c>MembershipAccessProvider</c> exposes membership
/// data across the same module boundary.
/// </summary>
internal sealed class CommerceVariantUsageProvider(ICommerceDbContext dbContext) : ICommerceVariantUsageProvider
{
    public async Task<CommerceVariantUsageSnapshot> GetUsageAsync(Guid variantId, CancellationToken cancellationToken = default)
    {
        var cartItemCount = await dbContext.CartItems
            .AsNoTracking()
            .CountAsync(c => c.ProductVariantId == variantId, cancellationToken);

        var orderItemCount = await dbContext.OrderItems
            .AsNoTracking()
            .CountAsync(i => i.ProductVariantId == variantId, cancellationToken);

        var orderLicenseKeyCount = await dbContext.OrderLicenseKeys
            .AsNoTracking()
            .CountAsync(k => k.ProductVariantId == variantId, cancellationToken);

        return new CommerceVariantUsageSnapshot(cartItemCount, orderItemCount, orderLicenseKeyCount);
    }

    public async Task<int> RemoveCartItemsAsync(Guid variantId, CancellationToken cancellationToken = default)
    {
        var items = await dbContext.CartItems
            .Where(c => c.ProductVariantId == variantId)
            .ToListAsync(cancellationToken);

        if (items.Count == 0)
        {
            return 0;
        }

        dbContext.CartItems.RemoveRange(items);
        await dbContext.SaveChangesAsync(cancellationToken);
        return items.Count;
    }
}
