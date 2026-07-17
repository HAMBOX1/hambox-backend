namespace HAMBOX.Modules.Suppliers.Application.Contracts;

public sealed record SupplierListItemDto(
    Guid Id,
    string Name,
    string Code,
    string ProviderType,
    string Status,
    int Priority,
    bool IsEnabled,
    string? BaseUrl,
    DateTimeOffset CreatedOnUtc);

public sealed record SupplierDetailDto(
    Guid Id,
    string Name,
    string Code,
    string ProviderType,
    string Status,
    int Priority,
    string? BaseUrl,
    string AuthenticationType,
    string? SettingsJson,
    string? Username,
    bool HasApiKey,
    bool HasApiSecret,
    bool HasPassword,
    bool HasBearerToken,
    bool HasOAuthSettings,
    bool SupportsInventorySync,
    bool SupportsPriceSync,
    bool SupportsReservations,
    bool SupportsOrderStatus,
    bool SupportsWebhooks,
    bool IsEnabled,
    DateTimeOffset CreatedOnUtc,
    DateTimeOffset? ModifiedOnUtc);

public sealed record CreateSupplierRequest(
    string Name,
    string Code,
    string ProviderType,
    string AuthenticationType,
    string? BaseUrl,
    int Priority,
    bool SupportsInventorySync,
    bool SupportsPriceSync,
    bool SupportsReservations,
    bool SupportsOrderStatus,
    bool SupportsWebhooks);

public sealed record UpdateSupplierRequest(
    string Name,
    string ProviderType,
    string AuthenticationType,
    string? BaseUrl,
    bool SupportsInventorySync,
    bool SupportsPriceSync,
    bool SupportsReservations,
    bool SupportsOrderStatus,
    bool SupportsWebhooks);

public sealed record UpdateSupplierCredentialsRequest(
    string? ApiKey,
    string? ApiSecret,
    string? Username,
    string? Password,
    string? BearerToken,
    string? OAuthSettingsJson);

public sealed record UpdateSupplierSettingsRequest(string? SettingsJson);

public sealed record UpdateSupplierPriorityRequest(int Priority);

public sealed record SupplierTestConnectionResultDto(bool IsSuccess, string Message);

public sealed record SupplierMappingDto(
    Guid Id,
    Guid SupplierId,
    Guid InternalProductId,
    string ExternalProductId,
    string? ExternalSku,
    string? ExternalName,
    decimal BuyingPrice,
    string Currency,
    int Priority,
    string Status,
    DateTimeOffset CreatedOnUtc);

public sealed record CreateSupplierMappingRequest(
    Guid InternalProductId,
    string ExternalProductId,
    string? ExternalSku,
    string? ExternalName,
    decimal BuyingPrice,
    string Currency,
    int Priority);

public sealed record UpdateSupplierMappingRequest(
    string ExternalProductId,
    string? ExternalSku,
    string? ExternalName,
    decimal BuyingPrice,
    string Currency,
    int Priority,
    string Status);
