using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HAMBOX.Modules.Catalog.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Catalog.Application.Features.Products;

/// <summary>
/// Shared "does this product already have its own variants" check used to block a product from
/// being merged — or parked as a pending merge — as a source, so the rule can't drift between
/// the immediate-merge and deferred-merge (pending merge) code paths.
/// </summary>
internal static class ProductMergeGuard
{
    public static Task<List<Guid>> GetProductIdsWithVariantsAsync(
        ICatalogDbContext db, IReadOnlyCollection<Guid> productIds, CancellationToken cancellationToken) =>
        db.ProductVariants
            .Where(v => !v.IsDeleted && productIds.Contains(v.ProductId))
            .Select(v => v.ProductId)
            .Distinct()
            .ToListAsync(cancellationToken);
}
