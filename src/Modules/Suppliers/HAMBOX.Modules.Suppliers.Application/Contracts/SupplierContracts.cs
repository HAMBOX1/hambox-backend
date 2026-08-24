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
    DateTimeOffset CreatedOnUtc,
    bool SupportsOrderStatus = false);

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

// ─── Map Products workspace ─────────────────────────────────────

/// <summary>One eligible HAMBOX product variant, alongside this supplier's own mapping for it (if any) —
/// the Map Products table's row shape. <see cref="ExistingMappingId"/> null means unmapped.</summary>
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

/// <summary>Stat-card counts for the Map Products header and the supplier detail page's Fulfillment
/// Health block — always computed from the same eligibility/mapping rules as
/// <see cref="SupplierMappingCandidateDto"/>, never a separate calculation.</summary>
public sealed record SupplierMappingCandidatesSummaryDto(int EligibleCount, int MappedCount, int UnmappedCount);

/// <summary>
/// Live auto-match result for one candidate. <see cref="BestMatch"/> is null exactly when
/// <see cref="ConfidenceTier"/> is <c>"None"</c> — never auto-created as a mapping, only ever a
/// suggestion the admin reviews or confirms.
/// </summary>
public sealed record SupplierMappingSuggestionDto(
    Guid ProductId,
    Guid VariantId,
    SupplierCatalogItemDto? BestMatch,
    int ConfidenceScore,
    string ConfidenceTier);

public sealed record BulkMappingFailureDto(Guid ProductId, Guid? VariantId, string ErrorCode, string ErrorMessage);

/// <summary>Result of the "Confirm N Mappings" bulk action — partial success is expected and normal (one
/// duplicate among several valid rows should never block the rest), so both lists are always populated
/// rather than an all-or-nothing outcome.</summary>
public sealed record BulkCreateSupplierMappingsResultDto(
    IReadOnlyList<Guid> CreatedMappingIds,
    IReadOnlyList<BulkMappingFailureDto> Failures);

/// <summary>Cross-supplier, business-friendly mapping status for one product on the admin product list —
/// see <c>GetSupplierMappingStatusForProductsQueryHandler</c> for the derivation rules.</summary>
public sealed record ProductSupplierMappingStatusDto(
    string Status,
    int MappedVariantCount,
    int TotalVariantCount,
    string? PrimarySupplierName);

/// <summary>
/// One variant's resolved mapping (if any), across every supplier — the product-centric counterpart to
/// <see cref="SupplierFulfillmentChainCandidateDto"/> (which is variant-centric, across suppliers, but
/// only ever one variant at a time). Backs both the product edit page's Supplier Fulfillment summary and
/// the product-centric mapping drawer's per-variant list, so the two can never disagree.
/// <see cref="MappingId"/> null means <see cref="MappingStatus"/> is <c>"Unmapped"</c>.
/// </summary>
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
