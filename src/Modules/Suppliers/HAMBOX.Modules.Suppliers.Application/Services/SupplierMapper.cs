using HAMBOX.Modules.Suppliers.Application.Abstractions;
using HAMBOX.Modules.Suppliers.Application.Contracts;
using HAMBOX.Modules.Suppliers.Domain.Suppliers;

namespace HAMBOX.Modules.Suppliers.Application.Services;

public static class SupplierMapper
{
    public static SupplierListItemDto ToListItem(Supplier supplier) => new(
        supplier.Id,
        supplier.Name,
        supplier.Code,
        supplier.ProviderType,
        supplier.Status.ToString(),
        supplier.Priority,
        supplier.IsEnabled,
        supplier.BaseUrl,
        supplier.CreatedOnUtc);

    public static SupplierDetailDto ToDetail(Supplier supplier) => new(
        supplier.Id,
        supplier.Name,
        supplier.Code,
        supplier.ProviderType,
        supplier.Status.ToString(),
        supplier.Priority,
        supplier.BaseUrl,
        supplier.AuthenticationType.ToString(),
        supplier.SettingsJson,
        supplier.Username,
        !string.IsNullOrEmpty(supplier.ApiKey),
        !string.IsNullOrEmpty(supplier.ApiSecret),
        !string.IsNullOrEmpty(supplier.Password),
        !string.IsNullOrEmpty(supplier.BearerToken),
        !string.IsNullOrEmpty(supplier.OAuthSettingsJson),
        supplier.SupportsInventorySync,
        supplier.SupportsPriceSync,
        supplier.SupportsReservations,
        supplier.SupportsOrderStatus,
        supplier.SupportsWebhooks,
        supplier.IsEnabled,
        supplier.CreatedOnUtc,
        supplier.ModifiedOnUtc);

    public static SupplierFulfillmentChainCandidateDto ToFulfillmentChainCandidate(
        SupplierProductMapping mapping, Supplier supplier, bool providerRegistered) => new(
        mapping.Id,
        supplier.Id,
        supplier.Name,
        supplier.ProviderType,
        mapping.InternalProductVariantId is null ? "ProductWide" : "VariantSpecific",
        mapping.ExternalProductId,
        mapping.Priority,
        mapping.Status.ToString(),
        supplier.IsEnabled,
        supplier.HasCredentialsConfigured,
        providerRegistered,
        supplier.IsEnabled && supplier.HasCredentialsConfigured && providerRegistered);

    public static SupplierMappingDto ToMappingDto(
        SupplierProductMapping mapping,
        string? internalProductName = null,
        string? internalVariantSku = null,
        SupplierProductAvailability? availability = null,
        decimal? defaultMarginPercent = null,
        Guid? selectedMappingIdForPricing = null)
    {
        var effectiveMarginPercent = mapping.MarginPercentOverride ?? defaultMarginPercent;
        var sellingPrice = effectiveMarginPercent is decimal margin
            ? mapping.BuyingPrice * (1 + margin / 100m)
            : (decimal?)null;

        return new(
            mapping.Id,
            mapping.SupplierId,
            mapping.InternalProductId,
            mapping.InternalProductVariantId,
            mapping.ExternalProductId,
            mapping.ExternalSku,
            mapping.ExternalName,
            mapping.BuyingPrice,
            mapping.Currency,
            mapping.Priority,
            mapping.Status.ToString(),
            mapping.CreatedOnUtc,
            internalProductName,
            internalVariantSku,
            availability?.AvailabilityState.ToString(),
            availability?.AvailableQuantity,
            availability?.LastCheckedAtUtc,
            mapping.MarginPercentOverride,
            effectiveMarginPercent,
            sellingPrice,
            selectedMappingIdForPricing == mapping.Id);
    }

    public static SupplierCatalogItemDto ToCatalogItemDto(SupplierCatalogItem item) => new(
        item.ExternalProductId,
        item.Name,
        item.BrandName,
        item.Currency,
        item.MinFaceValue,
        item.MaxFaceValue,
        item.Available);
}
