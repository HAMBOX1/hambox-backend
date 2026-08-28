using System.Globalization;
using System.Text.Json;
using HAMBOX.Modules.Suppliers.Application.Abstractions;
using HAMBOX.Modules.Suppliers.Domain.Fulfillments;
using Microsoft.Extensions.Logging;

namespace HAMBOX.Modules.Suppliers.Infrastructure.Providers.Bamboo;

/// <summary>
/// The first real automated <see cref="ISupplierProvider"/>. Every Bamboo-specific concept (its REST
/// endpoints, Basic Auth, request/response shapes, documented status/reason-code vocabulary) is
/// contained entirely in this file and the rest of <c>Providers/Bamboo/</c> — <see cref="ISupplierFulfillmentService"/>
/// and everything above it never sees any of it, only the generic <see cref="ISupplierProvider"/>
/// surface. A second automated supplier is exactly this same shape again, nothing here changes.
/// </summary>
internal sealed class BambooSupplierProvider(BambooHttpClient httpClient, ILogger<BambooSupplierProvider> logger) : ISupplierProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public string ProviderType => BambooProviderConstants.ProviderType;

    // No quantity cap is documented anywhere in the Bamboo API — Checkout's Products array already
    // accepts an arbitrary Quantity per line, so no cap is declared here.
    public int? MaxQuantityPerPurchase => null;

    public async Task<SupplierConnectionTestResult> TestConnectionAsync(SupplierProviderContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var accounts = await httpClient.GetAccountsAsync(context.Credentials, cancellationToken);
            return new SupplierConnectionTestResult(true, BuildConnectionSummary(accounts.Accounts));
        }
        catch (Exception ex) when (ex is BambooApiException or BambooAmbiguousResponseException)
        {
            logger.LogWarning(ex, "Bamboo connection test failed for supplier {SupplierId}.", context.SupplierId);
            return new SupplierConnectionTestResult(false, SafeMessage(ex));
        }
    }

    /// <summary>
    /// Only the safe, documented-non-secret fields from Get Accounts (id, currency, sandbox/production,
    /// active/inactive) — never <c>Balance</c> (financial detail beyond what the admin UI asked for) and
    /// never anything from <see cref="SupplierProviderCredentials"/>. Capped at 20 accounts so a supplier
    /// with an unusually large account list can't produce an unreasonably long UI message.
    /// </summary>
    private static string BuildConnectionSummary(IReadOnlyList<BambooAccount>? accounts)
    {
        var list = accounts ?? [];
        if (list.Count == 0)
        {
            return "Connected — 0 account(s) visible.";
        }

        var details = string.Join("; ", list.Take(20).Select(a =>
            $"Account {a.Id}: {(a.SandboxMode ? "Sandbox" : "Production")}, {a.Currency ?? "unknown currency"}, {(a.IsActive ? "Active" : "Inactive")}"));

        return $"Connected — {list.Count} account(s) visible. {details}";
    }

    public async Task<SupplierCredentialValidationResult> ValidateCredentialsAsync(SupplierProviderContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await httpClient.GetAccountsAsync(context.Credentials, cancellationToken);
            return new SupplierCredentialValidationResult(true, null);
        }
        catch (Exception ex) when (ex is BambooApiException or BambooAmbiguousResponseException)
        {
            logger.LogWarning(ex, "Bamboo credential validation failed for supplier {SupplierId}.", context.SupplierId);
            return new SupplierCredentialValidationResult(false, SafeMessage(ex));
        }
    }

    // Not part of the MVP purchase path — SupplierProductMapping already carries the Bamboo product id
    // per mapping, so catalog sync is a convenience feature, not a correctness dependency. Honest stub,
    // matching ManualSupplierProvider's pattern of reporting unsupported rather than silently no-op-ing.
    public Task<SupplierProductSyncResult> SyncProductsAsync(SupplierProviderContext context, CancellationToken cancellationToken = default) =>
        Task.FromResult(new SupplierProductSyncResult(false, 0, "Bamboo catalog sync is not implemented — map products manually via Supplier Product Mappings."));

    public async Task<SupplierCatalogSearchResult> SearchCatalogAsync(SupplierCatalogQuery query, SupplierProviderContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            // Bamboo's PageIndex is zero-based; SearchCatalogAsync's Page is the same 1-based convention every other paged query in this codebase uses.
            var zeroBasedPage = Math.Max(0, query.Page - 1);
            var response = await httpClient.GetCatalogAsync(context.Credentials, query.SearchTerm, zeroBasedPage, query.PageSize, cancellationToken);
            return new SupplierCatalogSearchResult(true, FlattenCatalog(response), null);
        }
        catch (Exception ex) when (ex is BambooApiException or BambooAmbiguousResponseException)
        {
            logger.LogWarning(ex, "Bamboo catalog search failed for supplier {SupplierId}.", context.SupplierId);
            return new SupplierCatalogSearchResult(false, [], SafeMessage(ex));
        }
    }

    /// <summary>
    /// Bamboo's catalog is brand-level entries each nesting their own denominations — flattened here
    /// into one selectable item per denomination, since that's the actual orderable unit (its <c>id</c>
    /// is what <see cref="BambooHttpClient.PlaceOrderAsync"/> takes as <c>ProductId</c>). Bamboo's schema
    /// has no separate SKU concept, so none is invented here.
    /// </summary>
    private static IReadOnlyList<SupplierCatalogItem> FlattenCatalog(BambooCatalogResponseBody response) =>
        (response.Items ?? [])
            .SelectMany(brand => (brand.Products ?? []).Select(product => new SupplierCatalogItem(
                product.Id.ToString(CultureInfo.InvariantCulture),
                CombineName(brand.Name, product.Name),
                brand.Name,
                product.Price?.CurrencyCode ?? brand.CurrencyCode ?? "USD",
                product.MinFaceValue ?? product.Price?.Min,
                product.MaxFaceValue ?? product.Price?.Max,
                product.Count is null or > 0)))
            .ToArray();

    private static string CombineName(string? brandName, string? productName)
    {
        if (!string.IsNullOrWhiteSpace(productName) && !string.Equals(productName, brandName, StringComparison.OrdinalIgnoreCase))
        {
            return string.IsNullOrWhiteSpace(brandName) ? productName! : $"{brandName} {productName}";
        }

        return brandName ?? productName ?? "Unknown product";
    }

    /// <summary>
    /// Reuses the exact same Get Catalog endpoint <see cref="SearchCatalogAsync"/> calls — never a
    /// second Bamboo API surface. Bamboo has no "give me exactly these product ids" endpoint, so this
    /// pulls the catalog unfiltered (no <c>Name</c> search term) in large pages and resolves every
    /// requested external id from the accumulated result, bounded at <see cref="MaxAvailabilityPages"/>
    /// pages — a small, fixed number of calls per sync tick for the WHOLE Bamboo catalog, never one
    /// call per mapping (see <see cref="ISupplierProvider.GetAvailabilityAsync"/>'s contract). Bamboo's
    /// catalog response has no literal "available" boolean field (confirmed against the real sandbox
    /// and the documentation) — the same <c>Count is null or &gt; 0</c> derivation <see cref="FlattenCatalog"/>
    /// already uses is reused here (via <see cref="FlattenCatalogForAvailability"/>, which keeps the raw
    /// <c>Count</c> too), so this and <see cref="SearchCatalogAsync"/> can never disagree about what
    /// "available" means for the same product.
    /// </summary>
    public async Task<SupplierAvailabilityResult> GetAvailabilityAsync(SupplierAvailabilityQuery query, SupplierProviderContext context, CancellationToken cancellationToken = default)
    {
        var requestedIds = query.ExternalProductIds.ToHashSet(StringComparer.Ordinal);
        if (requestedIds.Count == 0)
        {
            return new SupplierAvailabilityResult(true, [], null);
        }

        List<(string ExternalProductId, bool Available, int? Count)> catalogEntries;
        try
        {
            catalogEntries = await PullCatalogForAvailabilityAsync(context.Credentials, cancellationToken);
        }
        catch (Exception ex) when (ex is BambooApiException or BambooAmbiguousResponseException)
        {
            logger.LogWarning(ex, "Bamboo availability sync failed for supplier {SupplierId}.", context.SupplierId);
            return new SupplierAvailabilityResult(false, [], SafeMessage(ex));
        }

        var checkedAtUtc = DateTimeOffset.UtcNow;
        var byExternalId = catalogEntries
            .GroupBy(e => e.ExternalProductId, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        var items = requestedIds.Select(id => byExternalId.TryGetValue(id, out var entry)
                ? new SupplierAvailabilityItem(id, entry.Available ? SupplierAvailabilityState.Available : SupplierAvailabilityState.Unavailable, entry.Count, checkedAtUtc)
                // Not present anywhere in the pulled catalog pages — Bamboo has no per-id lookup to
                // fall back to, so per the provider contract this is a definite "not offered right now".
                : new SupplierAvailabilityItem(id, SupplierAvailabilityState.Unavailable, null, checkedAtUtc))
            .ToArray();

        return new SupplierAvailabilityResult(true, items, null);
    }

    /// <summary>Bounded so a catalog with unexpectedly many brands can never turn one sync tick into an unbounded number of HTTP calls.</summary>
    private const int MaxAvailabilityPages = 10;
    private const int AvailabilityPageSize = 200;

    private async Task<List<(string ExternalProductId, bool Available, int? Count)>> PullCatalogForAvailabilityAsync(
        SupplierProviderCredentials credentials, CancellationToken cancellationToken)
    {
        var entries = new List<(string, bool, int?)>();
        for (var page = 0; page < MaxAvailabilityPages; page++)
        {
            var response = await httpClient.GetCatalogAsync(credentials, searchTerm: null, page, AvailabilityPageSize, cancellationToken);
            entries.AddRange(FlattenCatalogForAvailability(response));

            // Bamboo's own page-size echo is the only reliable end-of-catalog signal available —
            // fewer brand entries than requested means this was the last page.
            if ((response.Items?.Count ?? 0) < AvailabilityPageSize)
            {
                break;
            }
        }

        return entries;
    }

    /// <summary>
    /// Same brand→denomination flattening as <see cref="FlattenCatalog"/>, kept as a small separate
    /// pass (rather than adding a quantity field to the shared, admin-facing <see cref="SupplierCatalogItem"/>
    /// DTO) so the availability path can carry Bamboo's raw <c>Count</c> through as
    /// <see cref="SupplierAvailabilityItem.AvailableQuantity"/> without changing the catalog-search
    /// contract admins already see. Deliberately never uses <c>MinFaceValue</c>/<c>MaxFaceValue</c> as
    /// quantity — those are a price range, not a stock count.
    /// </summary>
    private static IEnumerable<(string ExternalProductId, bool Available, int? Count)> FlattenCatalogForAvailability(BambooCatalogResponseBody response) =>
        (response.Items ?? [])
            .SelectMany(brand => (brand.Products ?? []).Select(product => (
                product.Id.ToString(CultureInfo.InvariantCulture),
                product.Count is null or > 0,
                product.Count)));

    public Task<SupplierInventorySyncResult> SyncInventoryAsync(SupplierProviderContext context, CancellationToken cancellationToken = default) =>
        Task.FromResult(new SupplierInventorySyncResult(false, 0, "Bamboo inventory sync is not implemented."));

    public Task<SupplierPriceSyncResult> SyncPricesAsync(SupplierProviderContext context, CancellationToken cancellationToken = default) =>
        Task.FromResult(new SupplierPriceSyncResult(false, 0, "Bamboo price sync is not implemented."));

    // Bamboo's documented API has no reservation step — Place Order purchases directly.
    public Task<SupplierReservationResult> ReserveAsync(SupplierReservationRequest request, SupplierProviderContext context, CancellationToken cancellationToken = default) =>
        Task.FromResult(new SupplierReservationResult(false, null, "Bamboo does not support reservations — purchase directly."));

    // No cancel/refund endpoint is documented anywhere in the Bamboo API — confirmed by a full-text
    // search of the documentation (see docs/integrations/suppliers/README.md). Reporting unsupported
    // rather than inventing one.
    public Task<SupplierCancellationResult> CancelAsync(SupplierCancellationRequest request, SupplierProviderContext context, CancellationToken cancellationToken = default) =>
        Task.FromResult(new SupplierCancellationResult(false, "Bamboo does not document a cancellation/refund API."));

    public async Task<SupplierPurchaseResult> PurchaseAsync(SupplierPurchaseRequest request, SupplierProviderContext context, CancellationToken cancellationToken = default)
    {
        var settings = ParseSettings(context.SettingsJson);
        if (settings?.AccountId is not long accountId)
        {
            return new SupplierPurchaseResult(false, null, null, SupplierFulfillmentFailureCategory.InvalidConfiguration,
                "No Bamboo AccountId configured for this supplier (Supplier.SettingsJson).");
        }

        if (!long.TryParse(request.ExternalProductId, out var productId))
        {
            return new SupplierPurchaseResult(false, null, null, SupplierFulfillmentFailureCategory.InvalidConfiguration,
                "The supplier product mapping's external product id is not a valid Bamboo numeric product id.");
        }

        if (request.UnitFaceValue is not decimal faceValue)
        {
            return new SupplierPurchaseResult(false, null, null, SupplierFulfillmentFailureCategory.InvalidConfiguration,
                "No face value configured on the supplier product mapping (BuyingPrice).");
        }

        if (!Guid.TryParse(request.ReferenceId, out var referenceGuid))
        {
            return new SupplierPurchaseResult(false, null, null, SupplierFulfillmentFailureCategory.InvalidConfiguration,
                "ReferenceId was not a valid GUID — cannot submit to Bamboo as RequestId.");
        }

        // No try/catch around BambooAmbiguousResponseException here — it (and any other unexpected
        // exception) propagates to the caller by design, per ISupplierProvider.PurchaseAsync's
        // documented ambiguity contract: an exception here means "unknown, resolve via GetOrderStatusAsync."
        try
        {
            var providerRequestId = await httpClient.PlaceOrderAsync(
                context.Credentials, referenceGuid, accountId, productId, request.Quantity, faceValue, cancellationToken);

            // Documented behavior: Place Order only confirms acceptance, never delivers codes
            // synchronously — DeliveredCodes stays null, driving the orchestrator's Submitted state
            // (not Succeeded) until GetOrderStatusAsync confirms an actual outcome.
            return new SupplierPurchaseResult(true, providerRequestId, DeliveredCodes: null, FailureCategory: null, Message: null);
        }
        catch (BambooApiException ex)
        {
            logger.LogWarning(
                "Bamboo purchase definitively rejected for HamboxReferenceId {HamboxReferenceId}: {ReasonCode} (HTTP {StatusCode}).",
                referenceGuid, ex.ReasonCode, ex.HttpStatusCode);
            return new SupplierPurchaseResult(false, null, null, MapFailureCategory(ex), SafeMessage(ex));
        }
    }

    public async Task<SupplierOrderStatusResult> GetOrderStatusAsync(SupplierOrderStatusQuery query, SupplierProviderContext context, CancellationToken cancellationToken = default)
    {
        // No try/catch: any failure here is caught by the orchestrator's ReconcileAsync, which treats
        // it as "still can't resolve, try again later" regardless of the specific cause.
        var order = await httpClient.GetOrderAsync(context.Credentials, query.HamboxReferenceId.ToString(), cancellationToken);

        var status = MapOrderStatus(order.Status);
        var deliveredCodes = ExtractDeliveredCodes(order);
        var providerOrderId = order.OrderId?.ToString();

        var failureCategory = status == SupplierProviderOrderStatus.Failed
            ? SupplierFulfillmentFailureCategory.UnknownProviderState // GetOrder's errorMessage is free text, not a machine-readable reason code — never guessed into a specific category.
            : (SupplierFulfillmentFailureCategory?)null;

        return new SupplierOrderStatusResult(status, providerOrderId, deliveredCodes, failureCategory, order.ErrorMessage);
    }

    private static BambooSupplierSettings? ParseSettings(string? settingsJson)
    {
        if (string.IsNullOrWhiteSpace(settingsJson))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<BambooSupplierSettings>(settingsJson, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static SupplierFulfillmentFailureCategory MapFailureCategory(BambooApiException ex)
    {
        if (ex.HttpStatusCode is 401 or 403)
        {
            return SupplierFulfillmentFailureCategory.AuthenticationFailed;
        }

        if (ex.HttpStatusCode == 429)
        {
            return SupplierFulfillmentFailureCategory.ProviderUnavailable;
        }

        return ex.ReasonCode switch
        {
            BambooReasonCode.InsufficientBalance => SupplierFulfillmentFailureCategory.InsufficientSupplierBalance,
            BambooReasonCode.ProductIsOutOfStock => SupplierFulfillmentFailureCategory.ProductUnavailable,
            BambooReasonCode.InvalidProductIds or BambooReasonCode.InvalidProduct or BambooReasonCode.ClientCatalogNotExist
                => SupplierFulfillmentFailureCategory.InvalidProduct,
            BambooReasonCode.InvalidDenomination or BambooReasonCode.ClientPriceInvalid
                => SupplierFulfillmentFailureCategory.InvalidDenomination,
            BambooReasonCode.WrongAccount or BambooReasonCode.NoProducts or BambooReasonCode.CardsLimitExceeded
                => SupplierFulfillmentFailureCategory.InvalidConfiguration,
            BambooReasonCode.UserNotEnabled => SupplierFulfillmentFailureCategory.AuthenticationFailed,
            // OrderAlreadyExists never reaches here — the HTTP client throws it as ambiguous instead.
            _ => SupplierFulfillmentFailureCategory.UnknownProviderState,
        };
    }

    private static SupplierProviderOrderStatus MapOrderStatus(string? status) => status switch
    {
        BambooOrderStatus.Created or BambooOrderStatus.Pending => SupplierProviderOrderStatus.Pending,
        BambooOrderStatus.Processing => SupplierProviderOrderStatus.Processing,
        BambooOrderStatus.Succeeded => SupplierProviderOrderStatus.Succeeded,
        BambooOrderStatus.PartialFailed => SupplierProviderOrderStatus.PartialFailed,
        BambooOrderStatus.Failed => SupplierProviderOrderStatus.Failed,
        _ => SupplierProviderOrderStatus.Unknown, // never guessed — an unrecognized/undocumented status string stays Unknown
    };

    private static IReadOnlyCollection<string>? ExtractDeliveredCodes(BambooOrderResponseBody order)
    {
        if (order.Items is null)
        {
            return null;
        }

        var codes = order.Items
            .SelectMany(item => item.Cards ?? [])
            .Where(card => string.Equals(card.Status, BambooCardStatus.Sold, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(card.CardCode))
            .Select(card => string.IsNullOrEmpty(card.Pin) ? card.CardCode! : $"{card.CardCode}:{card.Pin}")
            .ToArray();

        // Only Succeeded/PartialFailed statuses are ever interpreted using this collection by the
        // orchestrator (see SupplierOrderStatusResult's contract) — for any other status an empty
        // array here is harmless, so no special-casing is needed based on `order.Status`.
        return codes;
    }

    /// <summary>Never the raw exception/response — just the documented-safe message, never Authorization headers or credential values (neither ever reaches an exception message in this provider).</summary>
    private static string SafeMessage(Exception ex) => ex.Message;
}
