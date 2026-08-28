using HAMBOX.Application.Abstractions;
using HAMBOX.Application.PlatformSettings;
using HAMBOX.Modules.Commerce.Application.Abstractions;

namespace HAMBOX.Modules.Commerce.Infrastructure.Services;

/// <summary>
/// Implements <see cref="ISupplierPricingEngine"/> — see that interface for why margin application is
/// a separate layer on top of <see cref="ISupplierRoutingEngine"/> rather than a change to its own
/// cost-ascending ranking.
/// </summary>
internal sealed class SupplierPricingEngine(
    ISupplierRoutingEngine routingEngine,
    IPlatformSettingsProvider platformSettings)
    : ISupplierPricingEngine
{
    public async Task<SupplierPricingResult> ResolveAsync(SupplierRoutingRequest request, CancellationToken cancellationToken = default)
    {
        var routingResult = await routingEngine.ResolveAsync(request, cancellationToken);

        if (routingResult.EligibleByCostAscending.Count == 0)
        {
            return new SupplierPricingResult([], routingResult.Rejected, routingResult.BaseCurrency);
        }

        var commerceSettings = await platformSettings.GetAsync<CommerceSettingsPayload>(
            PlatformSettingsCategoryKeys.Commerce, cancellationToken);

        var priced = routingResult.EligibleByCostAscending
            .Select(c =>
            {
                var marginPercent = c.MarginPercentOverride ?? commerceSettings.DefaultSupplierMarginPercent;
                var sellingPrice = c.CostInBaseCurrency * (1 + marginPercent / 100m);

                return new SupplierPricingCandidate(
                    c.SupplierId,
                    c.SupplierName,
                    c.ProviderType,
                    c.SupplierProductMappingId,
                    c.CostInBaseCurrency,
                    sellingPrice,
                    marginPercent,
                    c.OriginalCost,
                    c.OriginalCurrency,
                    c.Priority);
            })
            .OrderBy(c => c.SellingPrice)
            .ThenBy(c => c.Priority)
            .ThenBy(c => c.SupplierId)
            .ToList();

        return new SupplierPricingResult(priced, routingResult.Rejected, routingResult.BaseCurrency);
    }
}
