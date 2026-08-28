using HAMBOX.Infrastructure.Currency;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Suppliers.Application.Abstractions;
using HAMBOX.Modules.Suppliers.Application.Options;
using HAMBOX.Modules.Suppliers.Domain.Suppliers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HAMBOX.Modules.Commerce.Infrastructure.Services;

/// <summary>
/// Implements <see cref="ISupplierRoutingEngine"/> — see that interface for why this lives in
/// <c>Commerce</c> rather than BuildingBlocks. Reuses exactly the same mapping-resolution shape as
/// <c>FulfillmentRouter</c> (variant-specific mapping preferred over product-wide, per supplier) plus
/// this module's shared <see cref="Application.Services.SupplierAvailabilityFreshness"/> check, so the
/// two engines can never disagree about "is this mapping available." Adds three things
/// <c>FulfillmentRouter</c> doesn't need: cost comparison (normalized via the existing
/// <see cref="CurrencyExchangeRateService"/> — the same one <c>DotFawryChargeAmountResolver</c> already
/// uses to actually charge a converted amount, not just display one), quantity-capability filtering
/// (<see cref="ISupplierProvider.MaxQuantityPerPurchase"/>), and a full rejected-candidates list for
/// admin audit — <c>FulfillmentRouter</c> only ever needs the single best candidate, never why every
/// other one was excluded.
/// </summary>
internal sealed class SupplierRoutingEngine(
    ISuppliersDbContext suppliersDb,
    ISupplierProviderRegistry providerRegistry,
    CurrencyExchangeRateService exchangeRateService,
    IOptions<SupplierAvailabilityOptions> availabilityOptions)
    : ISupplierRoutingEngine
{
    private const string ManualProviderType = "Manual";

    public async Task<SupplierRoutingResult> ResolveAsync(SupplierRoutingRequest request, CancellationToken cancellationToken = default)
    {
        var mappingRows = await (
            from mapping in suppliersDb.SupplierProductMappings.AsNoTracking()
            join supplier in suppliersDb.Suppliers.AsNoTracking() on mapping.SupplierId equals supplier.Id
            where mapping.InternalProductId == request.ProductId
                  && mapping.Status == SupplierMappingStatus.Active
                  && (mapping.InternalProductVariantId == request.VariantId || mapping.InternalProductVariantId == null)
            select new MappingRow(mapping, supplier))
            .ToListAsync(cancellationToken);

        // Collapse each supplier down to its own single best (most-specific) mapping for this exact
        // variant — a supplier must never compete against itself in the cost comparison just because it
        // has both a variant-specific and a product-wide mapping for the same product.
        var resolvedPerSupplier = mappingRows
            .GroupBy(r => r.Supplier.Id)
            .Select(g => g.OrderBy(r => r.Mapping.InternalProductVariantId is null ? 1 : 0).First())
            .ToList();

        if (resolvedPerSupplier.Count == 0)
        {
            return new SupplierRoutingResult([], [], await ResolveBaseCurrencyAsync(cancellationToken));
        }

        var mappingIds = resolvedPerSupplier.Select(r => r.Mapping.Id).ToList();
        var availabilityByMapping = await suppliersDb.SupplierProductAvailabilities.AsNoTracking()
            .Where(a => mappingIds.Contains(a.SupplierProductMappingId))
            .ToDictionaryAsync(a => a.SupplierProductMappingId, cancellationToken);

        var rates = await exchangeRateService.GetRatesAsync(cancellationToken);
        var staleAfter = TimeSpan.FromMinutes(availabilityOptions.Value.StaleAfterMinutes);
        var utcNow = DateTimeOffset.UtcNow;

        var eligible = new List<SupplierRoutingCandidate>();
        var rejected = new List<SupplierRoutingRejection>();

        foreach (var row in resolvedPerSupplier)
        {
            var reason = EvaluateEligibility(row, request.Quantity, availabilityByMapping, staleAfter, utcNow, rates, out var costInBaseCurrency);
            if (reason is not null)
            {
                rejected.Add(new SupplierRoutingRejection(row.Supplier.Id, row.Supplier.Name, row.Mapping.Id, reason));
                continue;
            }

            eligible.Add(new SupplierRoutingCandidate(
                row.Supplier.Id,
                row.Supplier.Name,
                row.Supplier.ProviderType,
                row.Mapping.Id,
                costInBaseCurrency!.Value,
                row.Mapping.BuyingPrice,
                row.Mapping.Currency,
                row.Mapping.Priority,
                row.Mapping.MarginPercentOverride));
        }

        var ranked = eligible
            .OrderBy(c => c.CostInBaseCurrency)
            .ThenBy(c => c.Priority)
            .ThenBy(c => c.SupplierId)
            .ToList();

        return new SupplierRoutingResult(ranked, rejected, rates.BaseCurrency);
    }

    /// <summary>Returns a safe rejection reason, or null (and sets <paramref name="costInBaseCurrency"/>) when the candidate is fully eligible.</summary>
    private string? EvaluateEligibility(
        MappingRow row,
        int quantity,
        IReadOnlyDictionary<Guid, SupplierProductAvailability> availabilityByMapping,
        TimeSpan staleAfter,
        DateTimeOffset utcNow,
        CurrencyRatesSnapshot rates,
        out decimal? costInBaseCurrency)
    {
        costInBaseCurrency = null;

        if (!row.Supplier.IsEnabled)
        {
            return "Supplier is disabled.";
        }

        if (!row.Supplier.HasCredentialsConfigured)
        {
            return "Supplier credentials are not configured.";
        }

        // The one honest-stub, non-purchasing provider is never a candidate for automated routing —
        // excluded here only (not in FulfillmentRouter's broader "readiness" concept), since that
        // component answers a different question ("is something ready") for a different caller
        // (checkout/storefront display) that this change must not alter.
        if (string.Equals(row.Supplier.ProviderType, ManualProviderType, StringComparison.OrdinalIgnoreCase))
        {
            return "Manual suppliers do not support automated purchase.";
        }

        var providerResolution = providerRegistry.Resolve(row.Supplier.ProviderType);
        if (providerResolution.IsFailure)
        {
            return "No provider is registered for this supplier's provider type.";
        }

        if (providerResolution.Value.MaxQuantityPerPurchase is int maxQuantity && quantity > maxQuantity)
        {
            return $"Supplier does not support the requested quantity ({quantity} > {maxQuantity} per purchase).";
        }

        if (!Application.Services.SupplierAvailabilityFreshness.IsAvailableAndFresh(availabilityByMapping.GetValueOrDefault(row.Mapping.Id), staleAfter, utcNow))
        {
            return "Not currently available (or availability has not been checked recently enough).";
        }

        if (row.Mapping.BuyingPrice <= 0)
        {
            return "No valid acquisition cost is configured on this mapping.";
        }

        var normalizedCost = NormalizeToBaseCurrency(row.Mapping.BuyingPrice, row.Mapping.Currency, rates);
        if (normalizedCost is null)
        {
            return $"No exchange rate is configured for currency '{row.Mapping.Currency}' — cannot compare cost.";
        }

        costInBaseCurrency = normalizedCost;
        return null;
    }

    /// <summary><paramref name="rates"/>.Rates values are "units of that currency per one base-currency unit" (see CurrencyExchangeRateService) — so converting FROM currency TO base is amount / rate.</summary>
    private static decimal? NormalizeToBaseCurrency(decimal amount, string currency, CurrencyRatesSnapshot rates)
    {
        if (string.Equals(currency, rates.BaseCurrency, StringComparison.OrdinalIgnoreCase))
        {
            return amount;
        }

        return rates.Rates.TryGetValue(currency, out var rate) && rate > 0
            ? amount / rate
            : null;
    }

    private async Task<string> ResolveBaseCurrencyAsync(CancellationToken cancellationToken)
    {
        var rates = await exchangeRateService.GetRatesAsync(cancellationToken);
        return rates.BaseCurrency;
    }

    private sealed record MappingRow(SupplierProductMapping Mapping, Supplier Supplier);
}
