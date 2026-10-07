using HAMBOX.Application.Variants;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Commerce.Application.Variants;

internal sealed class CommerceVariantRelocator(ICommerceDbContext dbContext) : ICommerceVariantRelocator
{
    public async Task MoveVariantAsync(Guid variantId, Guid newProductId, CancellationToken cancellationToken = default)
    {
        await dbContext.CartItems
            .Where(c => c.ProductVariantId == variantId)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.ProductId, newProductId), cancellationToken);

        await dbContext.CustomerAlertSubscriptions
            .Where(a => a.VariantId == variantId)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.ProductId, newProductId), cancellationToken);
    }
}
