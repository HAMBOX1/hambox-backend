using HAMBOX.Application.Fulfillment;
using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Catalog.Domain.Enums;
using HAMBOX.Modules.Suppliers.Application.Abstractions;
using HAMBOX.Modules.Suppliers.Application.Options;
using HAMBOX.Modules.Suppliers.Domain.Suppliers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HAMBOX.Modules.Commerce.Application.Services;

/// <summary>
/// Implements <see cref="IFulfillmentRouter"/> (contract in BuildingBlocks — see that interface's own
/// doc comment for why). Pure read query, no side effects, no external HTTP calls, safe to call at
/// checkout-validation time, storefront-display time, and post-payment alike. Callers
/// (<c>CartLineValidator</c>, <c>OrderFulfillmentService</c>, Catalog's storefront configuration
/// queries) decide what to DO with the answer; this never reserves inventory, never creates a
/// <c>SupplierFulfillment</c>, and never talks to a provider directly — it only reads
/// <c>ProductVariant</c>/<c>Supplier</c>/<c>SupplierProductMapping</c>/<c>SupplierProductAvailability</c>
/// and asks <see cref="ISupplierProviderRegistry"/> whether a provider type is registered.
/// </summary>
/// <remarks>
/// Distinguishes two separate concepts a candidate must satisfy, per the Supplier Availability phase:
/// READINESS ("can HAMBOX technically talk to this supplier" — enabled, credentials configured,
/// provider type registered) and AVAILABILITY ("does the persisted <c>SupplierProductAvailability</c>
/// cache say this specific mapped product is currently offered, and is that answer still fresh" — see
/// <see cref="IsAvailableAndFresh"/>). A candidate must satisfy both to be returned — this never calls a
/// provider live; availability is whatever the background sync last persisted.
/// </remarks>
internal sealed class FulfillmentRouter(
    ICatalogDbContext catalogDb,
    ISuppliersDbContext suppliersDb,
    ISupplierProviderRegistry providerRegistry,
    IOptions<SupplierAvailabilityOptions> availabilityOptions)
    : IFulfillmentRouter
{
    public async Task<FulfillmentReadiness> GetReadinessAsync(Guid variantId, CancellationToken cancellationToken = default)
    {
        var results = await GetReadinessBulkAsync([variantId], cancellationToken);
        return results.TryGetValue(variantId, out var readiness)
            ? readiness
            : new FulfillmentReadiness(FulfillmentMode.ManualOnly, true, null);
    }

    public async Task<IReadOnlyDictionary<Guid, FulfillmentReadiness>> GetReadinessBulkAsync(
        IEnumerable<Guid> variantIds, CancellationToken cancellationToken = default)
    {
        var ids = variantIds.Distinct().ToList();
        var result = new Dictionary<Guid, FulfillmentReadiness>();
        if (ids.Count == 0)
        {
            return result;
        }

        var variantRows = await catalogDb.ProductVariants.AsNoTracking()
            .Where(v => ids.Contains(v.Id))
            .Select(v => new { v.Id, v.ProductId, v.FulfillmentMode })
            .ToListAsync(cancellationToken);

        // No variant found is not this method's concern to fail on — callers already validate variant
        // existence themselves. Fail closed to the safest possible answer (manual only, no supplier)
        // rather than guessing; simply absent from the returned dictionary.
        var manualOnlyIds = variantRows.Where(v => v.FulfillmentMode == FulfillmentMode.ManualOnly).Select(v => v.Id).ToHashSet();
        var routedVariants = variantRows.Where(v => v.FulfillmentMode != FulfillmentMode.ManualOnly).ToList();

        foreach (var id in manualOnlyIds)
        {
            // Never even queries supplier mappings for a ManualOnly variant — nothing to resolve, and
            // this keeps the READY check for ManualOnly products free of any Suppliers-schema query.
            result[id] = new FulfillmentReadiness(FulfillmentMode.ManualOnly, true, null);
        }

        if (routedVariants.Count == 0)
        {
            return result;
        }

        var productIds = routedVariants.Select(v => v.ProductId).Distinct().ToList();

        var mappingRows = await (
            from mapping in suppliersDb.SupplierProductMappings.AsNoTracking()
            join supplier in suppliersDb.Suppliers.AsNoTracking() on mapping.SupplierId equals supplier.Id
            where productIds.Contains(mapping.InternalProductId)
                  && mapping.Status == SupplierMappingStatus.Active
                  && supplier.IsEnabled
            select new MappingRow(mapping, supplier))
            .ToListAsync(cancellationToken);

        var mappingsByProduct = mappingRows.ToLookup(r => r.Mapping.InternalProductId);

        var mappingIds = mappingRows.Select(r => r.Mapping.Id).ToList();
        var availabilityByMapping = mappingIds.Count == 0
            ? new Dictionary<Guid, SupplierProductAvailability>()
            : await suppliersDb.SupplierProductAvailabilities.AsNoTracking()
                .Where(a => mappingIds.Contains(a.SupplierProductMappingId))
                .ToDictionaryAsync(a => a.SupplierProductMappingId, cancellationToken);

        var staleAfter = TimeSpan.FromMinutes(availabilityOptions.Value.StaleAfterMinutes);
        var utcNow = DateTimeOffset.UtcNow;

        foreach (var variant in routedVariants)
        {
            var manualAllowed = variant.FulfillmentMode == FulfillmentMode.ManualFirst;

            var candidate = ResolveSupplierCandidate(mappingsByProduct[variant.ProductId], variant.Id, availabilityByMapping, staleAfter, utcNow);
            result[variant.Id] = new FulfillmentReadiness(variant.FulfillmentMode, manualAllowed, candidate);
        }

        return result;
    }

    private sealed record MappingRow(SupplierProductMapping Mapping, Supplier Supplier);

    /// <summary>
    /// Generic, provider-agnostic chain resolution — operates purely against <c>Supplier</c>/
    /// <c>SupplierProductMapping</c>/<c>SupplierProductAvailability</c>/<see cref="ISupplierProviderRegistry"/>.
    /// Nothing here ever branches on <c>Supplier.ProviderType</c>; a second, third, fourth automated
    /// supplier is resolved by this exact same code with zero changes.
    /// </summary>
    private FulfillmentSupplierCandidate? ResolveSupplierCandidate(
        IEnumerable<MappingRow> productMappingRows,
        Guid variantId,
        IReadOnlyDictionary<Guid, SupplierProductAvailability> availabilityByMapping,
        TimeSpan staleAfter,
        DateTimeOffset utcNow)
    {
        // Ordering (variant-specific before product-wide, then Priority) and the READY predicate both
        // touch Supplier.HasCredentialsConfigured — a computed property, not a mapped column — so this
        // step deliberately runs in memory after materializing the join, rather than inside the SQL
        // query where it could not translate.
        var candidateRows = productMappingRows
            .Where(r => r.Mapping.InternalProductVariantId == variantId || r.Mapping.InternalProductVariantId is null)
            .OrderBy(r => r.Mapping.InternalProductVariantId is null ? 1 : 0)
            .ThenBy(r => r.Mapping.Priority);

        foreach (var row in candidateRows)
        {
            if (!row.Supplier.HasCredentialsConfigured)
            {
                continue;
            }

            if (providerRegistry.Resolve(row.Supplier.ProviderType).IsFailure)
            {
                continue;
            }

            // READY (the two checks above) is necessary but no longer sufficient — the persisted
            // availability cache must also say this specific mapped product is currently offered, and
            // that answer must still be fresh. A mapping with no row yet (never synced) or a stale one
            // is treated identically to an explicit Unavailable — never optimistically assumed
            // available just because the supplier itself is reachable.
            if (!IsAvailableAndFresh(availabilityByMapping.GetValueOrDefault(row.Mapping.Id), staleAfter, utcNow))
            {
                continue;
            }

            return new FulfillmentSupplierCandidate(row.Supplier.Id, row.Mapping.Id);
        }

        return null;
    }

    private static bool IsAvailableAndFresh(SupplierProductAvailability? availability, TimeSpan staleAfter, DateTimeOffset utcNow)
    {
        if (availability is not { AvailabilityState: HAMBOX.Modules.Suppliers.Domain.Suppliers.SupplierAvailabilityState.Available, LastCheckedAtUtc: DateTimeOffset checkedAtUtc })
        {
            return false;
        }

        return utcNow - checkedAtUtc <= staleAfter;
    }
}
