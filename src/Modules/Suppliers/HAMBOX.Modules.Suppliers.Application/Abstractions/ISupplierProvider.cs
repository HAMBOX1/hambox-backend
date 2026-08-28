using HAMBOX.Modules.Suppliers.Domain.Fulfillments;

namespace HAMBOX.Modules.Suppliers.Application.Abstractions;

/// <summary>
/// The one contract every supplier integration implements. The marketplace core (Commerce/Catalog)
/// never references a concrete supplier — it resolves an instance via <see cref="ISupplierProviderRegistry"/>
/// and calls only these members. Nothing here assumes REST/HTTP: a future GraphQL, SOAP, CSV-drop, or
/// FTP-polling provider implements the exact same interface, so adding it never touches this contract
/// or any caller of it.
/// </summary>
public interface ISupplierProvider
{
    /// <summary>
    /// The key that <see cref="Domain.Suppliers.Supplier.ProviderType"/> must match for
    /// <see cref="ISupplierProviderRegistry"/> to resolve this instance (e.g. "Manual", future "Bamboo").
    /// </summary>
    string ProviderType { get; }

    /// <summary>
    /// The most units this provider's documented purchase API accepts in a single
    /// <see cref="PurchaseAsync"/> call, or <see langword="null"/> when no such cap is documented — never
    /// guessed. A provider-agnostic caller (e.g. a cheapest-supplier routing engine) uses this to exclude
    /// a candidate whose declared cap is below the requested quantity, without needing to know why the
    /// cap exists or branch on <see cref="ProviderType"/>. Purely descriptive: <see cref="PurchaseAsync"/>
    /// itself remains the authoritative enforcement (see e.g. GlobeTopper's own quantity check) — this
    /// property must never disagree with what that call actually accepts.
    /// </summary>
    int? MaxQuantityPerPurchase { get; }

    Task<SupplierConnectionTestResult> TestConnectionAsync(
        SupplierProviderContext context, CancellationToken cancellationToken = default);

    Task<SupplierCredentialValidationResult> ValidateCredentialsAsync(
        SupplierProviderContext context, CancellationToken cancellationToken = default);

    Task<SupplierProductSyncResult> SyncProductsAsync(
        SupplierProviderContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches the supplier's live product catalog (e.g. Bamboo's Get Catalog endpoint) for admin-facing
    /// product mapping selection — distinct from <see cref="SyncProductsAsync"/>, which is a bulk
    /// import/reconciliation concept and returns no item data. A provider with no browsable catalog
    /// (e.g. <c>Manual</c>) reports <see cref="SupplierCatalogSearchResult.IsSuccess"/> false with an
    /// explanatory message rather than an empty success, so the admin UI can tell "no catalog available"
    /// apart from "no results for this search."
    /// </summary>
    Task<SupplierCatalogSearchResult> SearchCatalogAsync(
        SupplierCatalogQuery query, SupplierProviderContext context, CancellationToken cancellationToken = default);

    Task<SupplierInventorySyncResult> SyncInventoryAsync(
        SupplierProviderContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// The provider's current, best-known answer to "is this mapped external product available right
    /// now" — distinct from <see cref="SearchCatalogAsync"/> (admin-facing browse/search) even though a
    /// catalog-backed provider may implement both on top of the same underlying call. Never called once
    /// per mapping by orchestrating code — <paramref name="query"/> carries every external id to resolve
    /// in one pass, and an implementation backed by a single bulk provider endpoint (e.g. Bamboo's
    /// catalog) MUST resolve all of them from as few provider calls as the provider's own API allows,
    /// never one call per id. A provider with no availability signal at all (e.g. <c>Manual</c>) reports
    /// <see cref="SupplierAvailabilityResult.IsSuccess"/> false with an explanatory message — callers
    /// must never interpret that as "unavailable," only as "no signal, treat as Unknown."
    /// </summary>
    Task<SupplierAvailabilityResult> GetAvailabilityAsync(
        SupplierAvailabilityQuery query, SupplierProviderContext context, CancellationToken cancellationToken = default);

    Task<SupplierPriceSyncResult> SyncPricesAsync(
        SupplierProviderContext context, CancellationToken cancellationToken = default);

    Task<SupplierReservationResult> ReserveAsync(
        SupplierReservationRequest request, SupplierProviderContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Submits a purchase. <see cref="SupplierPurchaseRequest.ReferenceId"/> is the caller's stable,
    /// never-regenerated idempotency reference (HAMBOX's <c>SupplierFulfillment.HamboxReferenceId</c>)
    /// — implementations MUST send it to the provider as the client-supplied idempotency/reference key
    /// wherever the provider's API supports one, so a retried call is safely deduplicated on the
    /// provider's side too.
    /// </summary>
    /// <remarks>
    /// Contract for ambiguous outcomes: if the call's result cannot be determined with confidence (a
    /// timeout, a connection failure, a response that cannot be parsed), the implementation MUST throw
    /// rather than return a result with <c>IsSuccess = false</c> — the caller treats a thrown exception
    /// as "unknown, resolve via <see cref="GetOrderStatusAsync"/>" and a returned <c>IsSuccess = false</c>
    /// as "the provider gave a definite negative answer." Conflating the two would risk either a silent
    /// double-purchase (treating an ambiguous case as safely retriable) or an order wrongly abandoned
    /// (treating a real acceptance as a definite failure).
    /// </remarks>
    Task<SupplierPurchaseResult> PurchaseAsync(
        SupplierPurchaseRequest request, SupplierProviderContext context, CancellationToken cancellationToken = default);

    Task<SupplierCancellationResult> CancelAsync(
        SupplierCancellationRequest request, SupplierProviderContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Queries the provider for the current outcome of a previously-submitted purchase. Always usable
    /// via <see cref="SupplierOrderStatusQuery.HamboxReferenceId"/> alone — <see cref="SupplierOrderStatusQuery.ProviderOrderId"/>
    /// may be <see langword="null"/> (e.g. the initial submission never returned one before failing
    /// ambiguously), so implementations that support looking a purchase up by client reference (as
    /// documented Bamboo behavior does) MUST fall back to that rather than requiring the provider id.
    /// </summary>
    Task<SupplierOrderStatusResult> GetOrderStatusAsync(
        SupplierOrderStatusQuery query, SupplierProviderContext context, CancellationToken cancellationToken = default);
}

/// <summary>
/// Everything a provider needs to act on behalf of one <c>Supplier</c> record, assembled by the
/// Application layer from decrypted entity data. Providers never receive a DbContext or the entity
/// itself — this is the seam that keeps provider implementations persistence-agnostic and testable.
/// </summary>
public sealed record SupplierProviderContext(
    Guid SupplierId,
    string SupplierCode,
    string? BaseUrl,
    SupplierProviderCredentials Credentials,
    string? SettingsJson);

public sealed record SupplierProviderCredentials(
    string? ApiKey,
    string? ApiSecret,
    string? Username,
    string? Password,
    string? BearerToken,
    string? OAuthSettingsJson);

public sealed record SupplierConnectionTestResult(bool IsSuccess, string Message);

public sealed record SupplierCredentialValidationResult(bool IsValid, string? Message);

public sealed record SupplierProductSyncResult(bool IsSuccess, int ProductsSynced, string? Message);

/// <summary>Admin-facing catalog search — <paramref name="SearchTerm"/> is matched provider-side (e.g. Bamboo's brand-name filter), never by pulling the whole catalog client-side.</summary>
public sealed record SupplierCatalogQuery(string? SearchTerm, int Page, int PageSize);

/// <summary>
/// One selectable supplier product/denomination — safe, display-only fields. <see cref="ExternalProductId"/>
/// is exactly the value that belongs in <c>SupplierProductMapping.ExternalProductId</c>; nothing here is a
/// secret or a raw provider response.
/// </summary>
public sealed record SupplierCatalogItem(
    string ExternalProductId,
    string Name,
    string? BrandName,
    string Currency,
    decimal? MinFaceValue,
    decimal? MaxFaceValue,
    bool Available);

public sealed record SupplierCatalogSearchResult(bool IsSuccess, IReadOnlyList<SupplierCatalogItem> Items, string? Message);

public sealed record SupplierInventorySyncResult(bool IsSuccess, int ItemsSynced, string? Message);

/// <summary>
/// Tri-state so "the provider was never asked" / "the provider was asked and said no" / "the provider
/// was asked and said yes" are never conflated — collapsing this into a bool would either show a
/// brand-new, never-synced mapping as falsely in stock or a genuinely-unreachable provider as falsely
/// out of stock. See <see cref="ISupplierProvider.GetAvailabilityAsync"/>'s remarks.
/// </summary>
public enum SupplierAvailabilityState
{
    Unknown = 0,
    Available = 1,
    Unavailable = 2,
}

public sealed record SupplierAvailabilityQuery(IReadOnlyList<string> ExternalProductIds);

/// <summary>
/// <paramref name="AvailableQuantity"/> is <see langword="null"/> whenever the provider genuinely
/// doesn't expose a number (most providers only ever say yes/no) — never fabricated. <paramref name="CheckedAtUtc"/>
/// is when the provider was actually asked, not when this record was constructed, so a caller that
/// caches this value can compute staleness from the real check time even if there's processing delay
/// before it's persisted.
/// </summary>
public sealed record SupplierAvailabilityItem(
    string ExternalProductId,
    SupplierAvailabilityState State,
    int? AvailableQuantity,
    DateTimeOffset CheckedAtUtc);

/// <summary>
/// <paramref name="IsSuccess"/> false means the provider could not be asked at all (network/auth/parse
/// failure) — <paramref name="Items"/> is empty in that case and the caller must treat every requested
/// external id as <see cref="SupplierAvailabilityState.Unknown"/>, never <c>Unavailable</c>. An id
/// present in <paramref name="Items"/> reflects a genuine, definite provider answer for that id.
/// </summary>
public sealed record SupplierAvailabilityResult(bool IsSuccess, IReadOnlyList<SupplierAvailabilityItem> Items, string? Message);

public sealed record SupplierPriceSyncResult(bool IsSuccess, int PricesSynced, string? Message);

public sealed record SupplierReservationRequest(string ExternalProductId, int Quantity, string? ReferenceId);

public sealed record SupplierReservationResult(bool IsSuccess, string? ProviderReservationId, string? Message);

/// <summary>
/// <paramref name="UnitFaceValue"/>/<paramref name="Currency"/> are the denomination a
/// denomination-based provider (gift cards, top-ups) needs per unit purchased — sourced from
/// <c>SupplierProductMapping.BuyingPrice</c>/<c>Currency</c> by the orchestrator, never guessed by a
/// provider. A provider that doesn't need a face value (e.g. a fixed-SKU digital good) simply ignores it.
/// </summary>
public sealed record SupplierPurchaseRequest(
    string ExternalProductId,
    int Quantity,
    string? ProviderReservationId,
    string? ReferenceId,
    decimal? UnitFaceValue = null,
    string? Currency = null);

/// <summary>
/// <paramref name="DeliveredCodes"/> is <see langword="null"/> when the provider only confirmed
/// acceptance of the request (outcome not yet known — e.g. Bamboo's Place Order, which never returns
/// codes synchronously) and non-null once the delivered quantity is actually known. An empty
/// (non-null) collection is a malformed/inconsistent response when <paramref name="IsSuccess"/> is
/// <see langword="true"/> and the caller must fail closed rather than trust it.
/// </summary>
public sealed record SupplierPurchaseResult(
    bool IsSuccess,
    string? ProviderOrderId,
    IReadOnlyCollection<string>? DeliveredCodes,
    SupplierFulfillmentFailureCategory? FailureCategory,
    string? Message);

public sealed record SupplierCancellationRequest(string ProviderOrderId, string? Reason);

public sealed record SupplierCancellationResult(bool IsSuccess, string? Message);

/// <summary>Looks a purchase up by <paramref name="HamboxReferenceId"/> — the same value originally sent as <c>SupplierPurchaseRequest.ReferenceId</c> — falling back to <paramref name="ProviderOrderId"/> only if a provider needs it.</summary>
public sealed record SupplierOrderStatusQuery(Guid HamboxReferenceId, string? ProviderOrderId);

/// <summary>
/// Normalized order lifecycle every provider maps its own vocabulary into. Deliberately small and
/// provider-agnostic — a new provider with different status names still reports one of these values,
/// so the orchestrator never branches on provider-specific strings.
/// </summary>
public enum SupplierProviderOrderStatus
{
    /// <summary>Accepted, fulfillment not yet started on the provider's side.</summary>
    Pending = 0,

    /// <summary>Actively being fulfilled by the provider; outcome not yet known.</summary>
    Processing = 1,

    /// <summary>Terminal: the full requested quantity was delivered.</summary>
    Succeeded = 2,

    /// <summary>Terminal: some, but not all, of the requested quantity was delivered.</summary>
    PartialFailed = 3,

    /// <summary>Terminal: nothing was delivered.</summary>
    Failed = 4,

    /// <summary>The provider itself cannot yet resolve the outcome — try reconciliation again later.</summary>
    Unknown = 5,
}

/// <summary>
/// See <see cref="SupplierPurchaseResult"/> remarks for the <paramref name="DeliveredCodes"/> contract
/// — the same rule applies here for a <paramref name="Status"/> of <see cref="SupplierProviderOrderStatus.Succeeded"/>
/// or <see cref="SupplierProviderOrderStatus.PartialFailed"/>. A failed status-query call itself (network
/// failure, unparsable response) must throw, per <see cref="ISupplierProvider.PurchaseAsync"/>'s ambiguity
/// contract — this result type only represents a call that completed and got a definite provider answer.
/// </summary>
public sealed record SupplierOrderStatusResult(
    SupplierProviderOrderStatus Status,
    string? ProviderOrderId,
    IReadOnlyCollection<string>? DeliveredCodes,
    SupplierFulfillmentFailureCategory? FailureCategory,
    string? Message);
