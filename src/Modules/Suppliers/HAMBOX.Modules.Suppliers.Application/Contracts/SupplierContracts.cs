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

/// <summary>
/// <see cref="InternalProductName"/>/<see cref="InternalVariantSku"/> are display-only enrichment
/// (looked up from Catalog, not stored on the mapping itself) so the admin mappings list can show real
/// product names instead of raw GUIDs — see <c>GetSupplierMappingsQueryHandler</c>. Null when the
/// referenced product/variant can no longer be found (e.g. deleted after the mapping was created); the
/// mapping itself still functions, the UI just falls back to showing the id.
/// </summary>
public sealed record SupplierMappingDto(
    Guid Id,
    Guid SupplierId,
    Guid InternalProductId,
    Guid? InternalProductVariantId,
    string ExternalProductId,
    string? ExternalSku,
    string? ExternalName,
    decimal BuyingPrice,
    string Currency,
    int Priority,
    string Status,
    DateTimeOffset CreatedOnUtc,
    string? InternalProductName = null,
    string? InternalVariantSku = null,
    /// <summary>Null exactly when Manual, or when no supplier row exists yet for this mapping — the frontend renders that the same as "Unknown".</summary>
    string? AvailabilityState = null,
    int? AvailableQuantity = null,
    DateTimeOffset? AvailabilityLastCheckedAtUtc = null);

/// <summary>
/// Counts for the supplier detail page's availability status section — always computed from the same
/// <c>SupplierProductAvailability</c> rows the storefront/checkout path reads via <c>FulfillmentRouter</c>,
/// never a second computation.
/// </summary>
public sealed record SupplierAvailabilitySummaryDto(
    int MappedCount,
    int AvailableCount,
    int UnavailableCount,
    int UnknownCount,
    DateTimeOffset? LastSyncedAtUtc);

public sealed record CreateSupplierMappingRequest(
    Guid InternalProductId,
    string ExternalProductId,
    string? ExternalSku,
    string? ExternalName,
    decimal BuyingPrice,
    string Currency,
    int Priority,
    Guid? InternalProductVariantId = null);

/// <summary>
/// One entry in a product/variant's fulfillment chain — safe metadata only (no credential values,
/// no raw settings JSON). <see cref="Scope"/> is <c>"VariantSpecific"</c> or <c>"ProductWide"</c>.
/// <see cref="IsReady"/> mirrors the same READY definition <c>FulfillmentRouter</c> uses:
/// supplier enabled, credentials configured, and provider type registered.
/// </summary>
public sealed record SupplierFulfillmentChainCandidateDto(
    Guid MappingId,
    Guid SupplierId,
    string SupplierName,
    string ProviderType,
    string Scope,
    string ExternalProductId,
    int Priority,
    string MappingStatus,
    bool SupplierEnabled,
    bool CredentialsConfigured,
    bool ProviderRegistered,
    bool IsReady);

public sealed record UpdateSupplierMappingRequest(
    string ExternalProductId,
    string? ExternalSku,
    string? ExternalName,
    decimal BuyingPrice,
    string Currency,
    int Priority,
    string Status);

/// <summary>One selectable supplier catalog product — safe display fields only, mirrors <c>ISupplierProvider.SupplierCatalogItem</c> at the HTTP boundary.</summary>
public sealed record SupplierCatalogItemDto(
    string ExternalProductId,
    string Name,
    string? BrandName,
    string Currency,
    decimal? MinFaceValue,
    decimal? MaxFaceValue,
    bool Available);

public sealed record SupplierCatalogSearchResultDto(
    bool IsSuccess,
    IReadOnlyList<SupplierCatalogItemDto> Items,
    string? Message);

/// <summary>One row of the Map Products workspace table — a HAMBOX product variant next to this supplier's resolved mapping, if any.</summary>
public sealed record SupplierMappingCandidateDto(
    Guid ProductId,
    string ProductName,
    string CategoryName,
    Guid VariantId,
    string VariantDisplayName,
    string VariantSku,
    Guid? ExistingMappingId,
    string? ExternalProductId,
    string? ExternalName,
    decimal? BuyingPrice,
    string? Currency,
    string? AvailabilityState);

public sealed record SupplierMappingCandidatesSummaryDto(
    int EligibleCount,
    int MappedCount,
    int UnmappedCount);

/// <summary>Result of live auto-matching one candidate against a supplier's catalog. <see cref="ConfidenceTier"/> is "High"/"Medium"/"None".</summary>
public sealed record SupplierMappingSuggestionDto(
    Guid ProductId,
    Guid VariantId,
    SupplierCatalogItemDto? BestMatch,
    int ConfidenceScore,
    string ConfidenceTier);

public sealed record BulkMappingFailureDto(
    Guid ProductId,
    Guid? VariantId,
    string ErrorCode,
    string ErrorMessage);

public sealed record BulkCreateSupplierMappingsResultDto(
    IReadOnlyList<Guid> CreatedMappingIds,
    IReadOnlyList<BulkMappingFailureDto> Failures);

/// <summary><see cref="Status"/> is one of "Unmapped"/"MappingError"/"SupplierUnavailable"/"PartiallyMapped"/"FullyMapped".</summary>
public sealed record ProductSupplierMappingStatusDto(
    string Status,
    int MappedVariantCount,
    int TotalVariantCount,
    string? PrimarySupplierName);

/// <summary>One product variant's resolved supplier mapping, or all-null fields when <see cref="MappingStatus"/> is "Unmapped".</summary>
public sealed record ProductVariantSupplierMappingDto(
    Guid VariantId,
    string VariantDisplayName,
    string VariantSku,
    Guid? MappingId,
    Guid? SupplierId,
    string? SupplierName,
    string? ExternalProductId,
    string? ExternalName,
    decimal? BuyingPrice,
    string? Currency,
    int? Priority,
    string? AvailabilityState,
    string MappingStatus);
