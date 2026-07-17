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

    public static SupplierMappingDto ToMappingDto(SupplierProductMapping mapping) => new(
        mapping.Id,
        mapping.SupplierId,
        mapping.InternalProductId,
        mapping.ExternalProductId,
        mapping.ExternalSku,
        mapping.ExternalName,
        mapping.BuyingPrice,
        mapping.Currency,
        mapping.Priority,
        mapping.Status.ToString(),
        mapping.CreatedOnUtc);
}
