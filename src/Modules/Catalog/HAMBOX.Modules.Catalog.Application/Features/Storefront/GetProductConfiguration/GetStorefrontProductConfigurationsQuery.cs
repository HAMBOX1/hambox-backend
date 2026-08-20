using HAMBOX.Application.Fulfillment;
using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Catalog.Application.Contracts;
using HAMBOX.Modules.Catalog.Domain.Enums;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Catalog.Application.Features.Storefront.GetProductConfiguration;

/// <summary>
/// Bulk form of <see cref="GetStorefrontProductConfigurationQuery"/> for product listing pages —
/// one round trip for the whole page of cards instead of one request per product. Ids that don't
/// resolve to an active product are silently omitted rather than failing the whole batch, mirroring
/// <c>GetPublishedProductMarketingPageAvailabilityQuery</c>'s partial-results contract.
/// </summary>
public sealed record GetStorefrontProductConfigurationsQuery(IReadOnlyList<Guid> ProductIds)
    : IRequest<Result<IReadOnlyList<StorefrontProductConfigurationDto>>>;

internal sealed class GetStorefrontProductConfigurationsQueryHandler
    : IRequestHandler<GetStorefrontProductConfigurationsQuery, Result<IReadOnlyList<StorefrontProductConfigurationDto>>>
{
    private readonly ICatalogDbContext _db;
    private readonly IInventoryEngine _inventoryEngine;
    private readonly IFulfillmentRouter _fulfillmentRouter;

    public GetStorefrontProductConfigurationsQueryHandler(ICatalogDbContext db, IInventoryEngine inventoryEngine, IFulfillmentRouter fulfillmentRouter)
    {
        _db = db;
        _inventoryEngine = inventoryEngine;
        _fulfillmentRouter = fulfillmentRouter;
    }

    public async Task<Result<IReadOnlyList<StorefrontProductConfigurationDto>>> Handle(
        GetStorefrontProductConfigurationsQuery request,
        CancellationToken cancellationToken)
    {
        var productIds = request.ProductIds.Distinct().ToList();
        if (productIds.Count == 0)
        {
            return Result.Success<IReadOnlyList<StorefrontProductConfigurationDto>>([]);
        }

        var products = await _db.Products
            .AsNoTracking()
            .Where(p => productIds.Contains(p.Id) && p.Status == ProductStatus.Active)
            .ToListAsync(cancellationToken);

        if (products.Count == 0)
        {
            return Result.Success<IReadOnlyList<StorefrontProductConfigurationDto>>([]);
        }

        var foundIds = products.Select(p => p.Id).ToList();

        var groups = await _db.ProductOptionGroups
            .AsNoTracking()
            .Include(g => g.Options)
            .Where(g => foundIds.Contains(g.ProductId))
            .OrderBy(g => g.SortOrder)
            .ToListAsync(cancellationToken);
        var groupsByProduct = groups.ToLookup(g => g.ProductId);

        var variants = await _db.ProductVariants
            .AsNoTracking()
            .Include(v => v.SelectedOptions)
            .Where(v => foundIds.Contains(v.ProductId) && v.Status == ProductVariantStatus.Active && v.IsVisible)
            .OrderBy(v => v.SortOrder)
            .ToListAsync(cancellationToken);
        var variantsByProduct = variants.ToLookup(v => v.ProductId);

        var stock = variants.Count > 0
            ? await _inventoryEngine.GetVariantStockBulkAsync(variants.Select(v => v.Id), cancellationToken)
            : new Dictionary<Guid, VariantStockSnapshot>();

        var readiness = variants.Count > 0
            ? await _fulfillmentRouter.GetReadinessBulkAsync(variants.Select(v => v.Id), cancellationToken)
            : new Dictionary<Guid, FulfillmentReadiness>();

        // Deliberately no ProductViewEvent logging here (unlike the single-product query) — loading
        // a page of listing cards is not the same signal as a customer opening a product's own page.

        var results = products
            .Select(product => StorefrontProductConfigurationBuilder.Build(
                product,
                groupsByProduct[product.Id].ToList(),
                variantsByProduct[product.Id].ToList(),
                stock,
                readiness))
            .ToList();

        return Result.Success<IReadOnlyList<StorefrontProductConfigurationDto>>(results);
    }
}
