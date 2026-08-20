using HAMBOX.Modules.Suppliers.Application.Abstractions;
using HAMBOX.Modules.Suppliers.Domain.Suppliers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using DomainAvailabilityState = HAMBOX.Modules.Suppliers.Domain.Suppliers.SupplierAvailabilityState;
using ProviderAvailabilityState = HAMBOX.Modules.Suppliers.Application.Abstractions.SupplierAvailabilityState;

namespace HAMBOX.Modules.Suppliers.Application.Services;

/// <summary>
/// See <see cref="ISupplierAvailabilityService"/> for the contract. Nothing here branches on
/// <c>Supplier.ProviderType</c> — the provider is always resolved generically via
/// <see cref="ISupplierProviderRegistry"/>, exactly like <c>SupplierFulfillmentService</c>.
/// </summary>
internal sealed class SupplierAvailabilityService(
    ISuppliersDbContext db,
    ISupplierProviderRegistry providerRegistry,
    ILogger<SupplierAvailabilityService> logger) : ISupplierAvailabilityService
{
    public async Task<IReadOnlyList<SupplierAvailabilitySyncResult>> SyncAllEnabledSuppliersAsync(CancellationToken cancellationToken = default)
    {
        var supplierIds = await db.Suppliers.AsNoTracking()
            .Where(s => s.IsEnabled)
            .Where(s => db.SupplierProductMappings.Any(m => m.SupplierId == s.Id && m.Status == SupplierMappingStatus.Active))
            .Select(s => s.Id)
            .ToListAsync(cancellationToken);

        var results = new List<SupplierAvailabilitySyncResult>(supplierIds.Count);
        foreach (var supplierId in supplierIds)
        {
            try
            {
                results.Add(await SyncSupplierAsync(supplierId, triggeredByUserId: null, cancellationToken));
            }
            catch (Exception ex)
            {
                // Defensive backstop only — SyncSupplierAsync itself already catches every expected
                // provider failure. One supplier's unexpected error must never stop the loop for the rest.
                logger.LogError(ex, "Unhandled error syncing availability for supplier {SupplierId}.", supplierId);
                results.Add(new SupplierAvailabilitySyncResult(supplierId, false, 0, 0, 0, 0, "Unexpected error — see server logs."));
            }
        }

        return results;
    }

    public async Task<SupplierAvailabilitySyncResult> SyncSupplierAsync(Guid supplierId, string? triggeredByUserId = null, CancellationToken cancellationToken = default)
    {
        var supplier = await db.Suppliers.AsNoTracking().FirstOrDefaultAsync(s => s.Id == supplierId, cancellationToken);
        if (supplier is null || !supplier.IsEnabled)
        {
            return new SupplierAvailabilitySyncResult(supplierId, true, 0, 0, 0, 0, "Supplier missing or disabled — nothing to sync.");
        }

        var mappings = await db.SupplierProductMappings.AsNoTracking()
            .Where(m => m.SupplierId == supplierId && m.Status == SupplierMappingStatus.Active)
            .ToListAsync(cancellationToken);

        if (mappings.Count == 0)
        {
            return new SupplierAvailabilitySyncResult(supplierId, true, 0, 0, 0, 0, "No active mappings — nothing to sync.");
        }

        var providerResolution = providerRegistry.Resolve(supplier.ProviderType);
        if (providerResolution.IsFailure)
        {
            return new SupplierAvailabilitySyncResult(supplierId, true, 0, 0, 0, 0, "No provider registered for this supplier's type — nothing to sync.");
        }

        var externalIds = mappings.Select(m => m.ExternalProductId).Distinct(StringComparer.Ordinal).ToList();
        var context = BuildContext(supplier);
        var provider = providerResolution.Value;

        SupplierAvailabilityResult providerResult;
        try
        {
            providerResult = await provider.GetAvailabilityAsync(new SupplierAvailabilityQuery(externalIds), context, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Availability provider call failed for supplier {SupplierId}.", supplierId);
            providerResult = new SupplierAvailabilityResult(false, [], "Provider call failed — see server logs.");
        }

        var mappingIds = mappings.Select(m => m.Id).ToList();
        var existingRows = await db.SupplierProductAvailabilities
            .Where(a => mappingIds.Contains(a.SupplierProductMappingId))
            .ToDictionaryAsync(a => a.SupplierProductMappingId, cancellationToken);

        var itemsByExternalId = providerResult.Items
            .GroupBy(i => i.ExternalProductId, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        int available = 0, unavailable = 0, unknown = 0;

        foreach (var mapping in mappings)
        {
            if (!existingRows.TryGetValue(mapping.Id, out var row))
            {
                row = SupplierProductAvailability.CreateUnknown(supplierId, mapping.Id, mapping.ExternalProductId);
                db.SupplierProductAvailabilities.Add(row);
                existingRows[mapping.Id] = row;
            }

            if (providerResult.IsSuccess && itemsByExternalId.TryGetValue(mapping.ExternalProductId, out var item))
            {
                row.RecordChecked(ToDomainState(item.State), item.AvailableQuantity, item.CheckedAtUtc, mapping.ExternalProductId);
            }
            else
            {
                // Either the whole provider call failed, or (defensively) this specific external id
                // wasn't in the result — either way, never overwrite the last known-good state, only
                // record that this attempt didn't produce a fresh answer.
                row.RecordSyncFailed(providerResult.Message ?? "External product id was not returned by the provider.");
            }

            switch (row.AvailabilityState)
            {
                case DomainAvailabilityState.Available: available++; break;
                case DomainAvailabilityState.Unavailable: unavailable++; break;
                default: unknown++; break;
            }
        }

        SupplierAuditWriter.Record(
            db, supplierId, SupplierAuditAction.AvailabilitySynced, actorUserId: triggeredByUserId,
            $"{{\"mappingsChecked\":{mappings.Count},\"available\":{available},\"unavailable\":{unavailable},\"unknown\":{unknown},\"providerSuccess\":{providerResult.IsSuccess.ToString().ToLowerInvariant()}}}");

        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Supplier availability sync: supplier {SupplierId} ({SupplierCode}, {ProviderType}) — {MappingsChecked} mapping(s), " +
            "{Available} available, {Unavailable} unavailable, {Unknown} unknown, provider call succeeded={ProviderSuccess}.",
            supplierId, supplier.Code, supplier.ProviderType, mappings.Count, available, unavailable, unknown, providerResult.IsSuccess);

        return new SupplierAvailabilitySyncResult(supplierId, providerResult.IsSuccess, mappings.Count, available, unavailable, unknown, providerResult.Message);
    }

    private static DomainAvailabilityState ToDomainState(ProviderAvailabilityState state) => state switch
    {
        ProviderAvailabilityState.Available => DomainAvailabilityState.Available,
        ProviderAvailabilityState.Unavailable => DomainAvailabilityState.Unavailable,
        _ => DomainAvailabilityState.Unknown,
    };

    private static SupplierProviderContext BuildContext(Supplier supplier) => new(
        supplier.Id,
        supplier.Code,
        supplier.BaseUrl,
        new SupplierProviderCredentials(supplier.ApiKey, supplier.ApiSecret, supplier.Username, supplier.Password, supplier.BearerToken, supplier.OAuthSettingsJson),
        supplier.SettingsJson);
}
