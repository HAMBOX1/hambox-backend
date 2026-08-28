using HAMBOX.Modules.Suppliers.Application.Abstractions;
using HAMBOX.Modules.Suppliers.Domain.Fulfillments;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace HAMBOX.Modules.Suppliers.Infrastructure.Providers.Visoria;

/// <summary>
/// The second real automated <see cref="ISupplierProvider"/>, following <c>BambooSupplierProvider</c>'s
/// exact shape. Every Visoria-specific concept (its REST endpoints, Bearer auth, request/response
/// shapes, documented status vocabulary) is contained entirely in this file and the rest of
/// <c>Providers/Visoria/</c> — <see cref="ISupplierFulfillmentService"/> and everything above it never
/// sees any of it, only the generic <see cref="ISupplierProvider"/> surface.
/// </summary>
internal sealed class VisoriaSupplierProvider(VisoriaHttpClient httpClient, IMemoryCache cache, ILogger<VisoriaSupplierProvider> logger) : ISupplierProvider
{
    public string ProviderType => VisoriaProviderConstants.ProviderType;

    // No quantity cap is documented anywhere in the Visoria API — an order line already accepts an
    // arbitrary Quantity, so no cap is declared here.
    public int? MaxQuantityPerPurchase => null;

    public async Task<SupplierConnectionTestResult> TestConnectionAsync(SupplierProviderContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var balances = await httpClient.GetBalanceAsync(context.Credentials, cancellationToken);
            return new SupplierConnectionTestResult(true, BuildConnectionSummary(balances));
        }
        catch (Exception ex) when (ex is VisoriaApiException or VisoriaAmbiguousResponseException)
        {
            logger.LogWarning(ex, "Visoria connection test failed for supplier {SupplierId}.", context.SupplierId);
            return new SupplierConnectionTestResult(false, SafeMessage(ex));
        }
    }

    /// <summary>Currency codes and live/test mode only — never the actual balance amount, mirroring <c>BambooSupplierProvider.BuildConnectionSummary</c>'s identical "no financial detail" choice.</summary>
    private static string BuildConnectionSummary(IReadOnlyList<VisoriaBalance>? balances)
    {
        var list = balances ?? [];
        if (list.Count == 0)
        {
            return "Connected — 0 currency balance(s) visible.";
        }

        var details = string.Join("; ", list.Take(20).Select(b => $"{b.CurrencyCode ?? "unknown currency"} ({(b.Livemode ? "live" : "test")})"));
        return $"Connected — {list.Count} currency balance(s) visible. {details}";
    }

    public async Task<SupplierCredentialValidationResult> ValidateCredentialsAsync(SupplierProviderContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await httpClient.GetBalanceAsync(context.Credentials, cancellationToken);
            return new SupplierCredentialValidationResult(true, null);
        }
        catch (Exception ex) when (ex is VisoriaApiException or VisoriaAmbiguousResponseException)
        {
            logger.LogWarning(ex, "Visoria credential validation failed for supplier {SupplierId}.", context.SupplierId);
            return new SupplierCredentialValidationResult(false, SafeMessage(ex));
        }
    }

    // Not part of the MVP purchase path — SupplierProductMapping already carries Visoria's product id
    // per mapping. Honest stub, matching ManualSupplierProvider/BambooSupplierProvider's identical convention.
    public Task<SupplierProductSyncResult> SyncProductsAsync(SupplierProviderContext context, CancellationToken cancellationToken = default) =>
        Task.FromResult(new SupplierProductSyncResult(false, 0, "Visoria catalog sync is not implemented — map products manually via Supplier Product Mappings."));

    public async Task<SupplierCatalogSearchResult> SearchCatalogAsync(SupplierCatalogQuery query, SupplierProviderContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            // Visoria's List Products endpoint has no server-side name/text search parameter (confirmed
            // against the OpenAPI spec's parameter list — unlike Bamboo's documented `Name` filter) —
            // pulled in bounded pages and filtered client-side here, the same bounded-pull shape
            // GetAvailabilityAsync already uses below for the identical "no per-term lookup" situation.
            // Cached briefly per supplier (search only — GetAvailabilityAsync below always pulls fresh):
            // the admin UI's search box fires one request per keystroke, and without this every keystroke
            // re-pulled the whole catalog (up to MaxCatalogPages real HTTP calls to Visoria each time),
            // which is both slow and quick to trip Visoria's own rate limiting.
            var products = await GetCachedCatalogAsync(context, cancellationToken);

            IEnumerable<VisoriaProduct> filtered = products;
            if (!string.IsNullOrWhiteSpace(query.SearchTerm))
            {
                filtered = filtered.Where(p => p.Name?.Contains(query.SearchTerm, StringComparison.OrdinalIgnoreCase) == true);
            }

            var items = filtered
                // Recharge products need per-unit customer account data (recharge_data) this integration
                // never collects — excluded here so an admin can't map one through this search UI only to
                // have every purchase attempt fail closed at PurchaseAsync (see its own remarks).
                .Where(p => !string.Equals(p.FulfillmentType, VisoriaFulfillmentType.Recharge, StringComparison.OrdinalIgnoreCase))
                .Skip((Math.Max(1, query.Page) - 1) * Math.Max(1, query.PageSize))
                .Take(Math.Max(1, query.PageSize))
                .Select(ToCatalogItem)
                .ToArray();

            return new SupplierCatalogSearchResult(true, items, null);
        }
        catch (Exception ex) when (ex is VisoriaApiException or VisoriaAmbiguousResponseException)
        {
            logger.LogWarning(ex, "Visoria catalog search failed for supplier {SupplierId}.", context.SupplierId);
            return new SupplierCatalogSearchResult(false, [], SafeMessage(ex));
        }
    }

    /// <summary>
    /// For a fixed-price (non-OPEN) product, Visoria's own <c>denomination.min/max</c> always describes
    /// the internal quantity unit (fixed at 1), not the real face value — <c>market_price</c> is the
    /// actual denomination admins need to see when choosing a mapping, so it's used for both bounds in
    /// that case. Only a genuine OPEN (variable-amount) product uses <c>denomination.min/max</c>.
    /// </summary>
    private static SupplierCatalogItem ToCatalogItem(VisoriaProduct product)
    {
        var isOpen = string.Equals(product.Denomination?.Type, VisoriaDenominationType.Open, StringComparison.OrdinalIgnoreCase);

        return new SupplierCatalogItem(
            product.Id ?? string.Empty,
            product.Name ?? "Unknown product",
            product.Categories?.FirstOrDefault()?.Name,
            product.CurrencyCode ?? "USD",
            isOpen ? product.Denomination?.Min : product.MarketPrice,
            isOpen ? product.Denomination?.Max : product.MarketPrice,
            product.Orderable && (product.StockUnlimited || product.Stock > 0));
    }

    /// <summary>Bounded so an unexpectedly large catalog can never turn one call into an unbounded number of HTTP requests.</summary>
    private const int MaxCatalogPages = 10;

    /// <summary>How long a pulled catalog stays fresh enough to reuse across the search box's rapid-fire
    /// keystroke requests. Short on purpose — this only smooths out one interactive search session, it's
    /// never relied on for the availability sync's own correctness (that path never reads this cache).</summary>
    private static readonly TimeSpan SearchCatalogCacheTtl = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Search-only cache in front of <see cref="PullCatalogAsync"/> — <see cref="GetAvailabilityAsync"/>
    /// still calls <see cref="PullCatalogAsync"/> directly and always gets a live pull, so this can never
    /// make availability/fulfillment sync see stale data. Keyed per supplier since each supplier carries
    /// its own credentials and could plausibly resolve to a different Visoria account/catalog.
    /// </summary>
    private Task<List<VisoriaProduct>> GetCachedCatalogAsync(SupplierProviderContext context, CancellationToken cancellationToken)
    {
        var cacheKey = $"visoria:search-catalog:{context.SupplierId}";
        return cache.GetOrCreateAsync(cacheKey, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = SearchCatalogCacheTtl;
            return PullCatalogAsync(context.Credentials, cancellationToken);
        })!;
    }

    private async Task<List<VisoriaProduct>> PullCatalogAsync(SupplierProviderCredentials credentials, CancellationToken cancellationToken)
    {
        var products = new List<VisoriaProduct>();
        for (var page = 1; page <= MaxCatalogPages; page++)
        {
            var response = await httpClient.GetProductsAsync(credentials, page, VisoriaProviderConstants.MaxPageSize, cancellationToken);
            var pageItems = response.Data ?? [];
            products.AddRange(pageItems);

            if (pageItems.Count < VisoriaProviderConstants.MaxPageSize)
            {
                break;
            }
        }

        return products;
    }

    public async Task<SupplierAvailabilityResult> GetAvailabilityAsync(SupplierAvailabilityQuery query, SupplierProviderContext context, CancellationToken cancellationToken = default)
    {
        var requestedIds = query.ExternalProductIds.ToHashSet(StringComparer.Ordinal);
        if (requestedIds.Count == 0)
        {
            return new SupplierAvailabilityResult(true, [], null);
        }

        List<VisoriaProduct> products;
        try
        {
            products = await PullCatalogAsync(context.Credentials, cancellationToken);
        }
        catch (Exception ex) when (ex is VisoriaApiException or VisoriaAmbiguousResponseException)
        {
            logger.LogWarning(ex, "Visoria availability sync failed for supplier {SupplierId}.", context.SupplierId);
            return new SupplierAvailabilityResult(false, [], SafeMessage(ex));
        }

        var checkedAtUtc = DateTimeOffset.UtcNow;
        var byId = products
            .Where(p => !string.IsNullOrEmpty(p.Id))
            .GroupBy(p => p.Id!, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        var items = requestedIds.Select(id => byId.TryGetValue(id, out var product)
                ? new SupplierAvailabilityItem(id, ResolveAvailability(product), product.StockUnlimited ? null : product.Stock, checkedAtUtc)
                // Not present anywhere in the pulled pages — Visoria has no per-id-batch lookup to fall
                // back to, so per the provider contract this is a definite "not offered right now".
                : new SupplierAvailabilityItem(id, SupplierAvailabilityState.Unavailable, null, checkedAtUtc))
            .ToArray();

        return new SupplierAvailabilityResult(true, items, null);
    }

    /// <summary>
    /// Recharge products require per-unit customer data this integration cannot supply — reported
    /// unavailable via this route even when Visoria itself considers them orderable, since a purchase
    /// attempt against one always fails closed in <see cref="PurchaseAsync"/>.
    /// </summary>
    private static SupplierAvailabilityState ResolveAvailability(VisoriaProduct product)
    {
        if (string.Equals(product.FulfillmentType, VisoriaFulfillmentType.Recharge, StringComparison.OrdinalIgnoreCase))
        {
            return SupplierAvailabilityState.Unavailable;
        }

        return product.Orderable && (product.StockUnlimited || product.Stock > 0)
            ? SupplierAvailabilityState.Available
            : SupplierAvailabilityState.Unavailable;
    }

    public Task<SupplierInventorySyncResult> SyncInventoryAsync(SupplierProviderContext context, CancellationToken cancellationToken = default) =>
        Task.FromResult(new SupplierInventorySyncResult(false, 0, "Visoria inventory sync is not implemented."));

    public Task<SupplierPriceSyncResult> SyncPricesAsync(SupplierProviderContext context, CancellationToken cancellationToken = default) =>
        Task.FromResult(new SupplierPriceSyncResult(false, 0, "Visoria price sync is not implemented."));

    // Visoria's documented API has no reservation step — Create Order purchases directly.
    public Task<SupplierReservationResult> ReserveAsync(SupplierReservationRequest request, SupplierProviderContext context, CancellationToken cancellationToken = default) =>
        Task.FromResult(new SupplierReservationResult(false, null, "Visoria does not support reservations — purchase directly."));

    // No customer-facing cancel/refund request endpoint is documented — `refund_status` on an order is
    // read-only reporting, not something this API lets a caller trigger. Reporting unsupported rather
    // than inventing one, matching BambooSupplierProvider's identical honest-stub choice.
    public Task<SupplierCancellationResult> CancelAsync(SupplierCancellationRequest request, SupplierProviderContext context, CancellationToken cancellationToken = default) =>
        Task.FromResult(new SupplierCancellationResult(false, "Visoria does not document a cancellation/refund request API."));

    public async Task<SupplierPurchaseResult> PurchaseAsync(SupplierPurchaseRequest request, SupplierProviderContext context, CancellationToken cancellationToken = default)
    {
        // Visoria's Idempotency-Key header requires 16-128 characters; HamboxReferenceId is always a
        // GUID string (32-36 chars) in practice, but this is checked rather than assumed.
        if (string.IsNullOrWhiteSpace(request.ReferenceId) || request.ReferenceId.Length is < 16 or > 128)
        {
            return new SupplierPurchaseResult(false, null, null, SupplierFulfillmentFailureCategory.InvalidConfiguration,
                "ReferenceId is missing or not a valid Visoria idempotency key length (16-128 characters).");
        }

        if (string.IsNullOrWhiteSpace(request.Currency))
        {
            return new SupplierPurchaseResult(false, null, null, SupplierFulfillmentFailureCategory.InvalidConfiguration,
                "No currency configured on the supplier product mapping (Currency) — required for Visoria orders.");
        }

        // No try/catch around VisoriaAmbiguousResponseException here — it (and any other unexpected
        // exception) propagates to the caller by design, per ISupplierProvider.PurchaseAsync's
        // documented ambiguity contract: an exception here means "unknown, resolve via GetOrderStatusAsync."
        try
        {
            // Visoria requires knowing whether the product is OPEN (variable face_value) or fixed
            // (face_value must be exactly 1) before an order can be built correctly — this is not
            // something SupplierProductMapping's generic BuyingPrice/Currency fields can distinguish on
            // their own, so it's resolved live against Visoria's own source of truth rather than guessed.
            var product = await httpClient.GetProductAsync(context.Credentials, request.ExternalProductId, cancellationToken);

            if (string.Equals(product.FulfillmentType, VisoriaFulfillmentType.Recharge, StringComparison.OrdinalIgnoreCase))
            {
                return new SupplierPurchaseResult(false, null, null, SupplierFulfillmentFailureCategory.InvalidProduct,
                    "Visoria recharge products require per-unit customer account data (recharge_data) that this integration does not collect — not supported.");
            }

            if (!product.Orderable)
            {
                return new SupplierPurchaseResult(false, null, null, SupplierFulfillmentFailureCategory.ProductUnavailable,
                    "Product is not currently orderable on Visoria.");
            }

            decimal faceValue;
            if (string.Equals(product.Denomination?.Type, VisoriaDenominationType.Open, StringComparison.OrdinalIgnoreCase))
            {
                if (request.UnitFaceValue is not decimal openFaceValue)
                {
                    return new SupplierPurchaseResult(false, null, null, SupplierFulfillmentFailureCategory.InvalidConfiguration,
                        "No face value configured on the supplier product mapping (BuyingPrice) — required for this OPEN-denomination Visoria product.");
                }

                faceValue = openFaceValue;
            }
            else
            {
                // Every non-OPEN product (including RECHARGE, already rejected above) requires exactly
                // 1 — the real price is server-side; sending anything else is rejected by Visoria's own validation.
                faceValue = 1m;
            }

            var body = new VisoriaCreateOrderRequestBody(
                [new VisoriaOrderLineItem(request.ExternalProductId, request.Quantity, faceValue)],
                request.Currency);

            var order = await httpClient.CreateOrderAsync(context.Credentials, request.ReferenceId, body, cancellationToken);
            var (_, deliveredCodes) = MapOrder(order);

            // Unlike Bamboo (which never returns codes synchronously), Visoria's Create Order can and
            // does complete synchronously — DeliveredCodes reflects whatever MapOrder actually resolved
            // (null while PROGRESSING/outcome unknown, a real collection once COMPLETED/CANCELLED).
            return new SupplierPurchaseResult(true, order.Id, deliveredCodes, null, null);
        }
        catch (VisoriaApiException ex)
        {
            logger.LogWarning(
                "Visoria purchase definitively rejected for HamboxReferenceId {HamboxReferenceId}: HTTP {StatusCode} {Code}.",
                request.ReferenceId, ex.HttpStatusCode, ex.Code);
            return new SupplierPurchaseResult(false, null, null, MapFailureCategory(ex), SafeMessage(ex));
        }
    }

    public async Task<SupplierOrderStatusResult> GetOrderStatusAsync(SupplierOrderStatusQuery query, SupplierProviderContext context, CancellationToken cancellationToken = default)
    {
        // Always resolved by HamboxReferenceId (Visoria's own idempotency key) via the documented
        // "get order by idempotency key" lookup — never ProviderOrderId — so this also closes the same
        // crash-recovery gap BambooSupplierProvider's identical choice does: a worker that claimed a
        // fulfillment but crashed before ever recording a ProviderOrderId is still fully recoverable.
        // No try/catch: any failure here is caught by the orchestrator's ReconcileAsync, which treats
        // it as "still can't resolve, try again later" regardless of the specific cause.
        var order = await httpClient.GetOrderByIdempotencyKeyAsync(context.Credentials, query.HamboxReferenceId.ToString(), cancellationToken);

        var (status, deliveredCodes) = MapOrder(order);
        var failureCategory = status == SupplierProviderOrderStatus.Failed
            ? SupplierFulfillmentFailureCategory.UnknownProviderState // Visoria's order shape carries no machine-readable failure reason — never guessed into a specific category.
            : (SupplierFulfillmentFailureCategory?)null;

        return new SupplierOrderStatusResult(status, order.Id, deliveredCodes, failureCategory, null);
    }

    /// <summary>
    /// Shared by <see cref="PurchaseAsync"/> and <see cref="GetOrderStatusAsync"/> so both can never
    /// disagree about what a given Visoria order shape means. PROGRESSING keeps <c>DeliveredCodes</c>
    /// null (outcome genuinely not yet known, per the shared contract) — COMPLETED/CANCELLED always
    /// resolve to a real (possibly empty) collection, since the outcome is definite at that point.
    /// </summary>
    private static (SupplierProviderOrderStatus Status, IReadOnlyCollection<string>? DeliveredCodes) MapOrder(VisoriaOrder order)
    {
        if (string.Equals(order.Status, VisoriaOrderStatus.Progressing, StringComparison.OrdinalIgnoreCase))
        {
            return (SupplierProviderOrderStatus.Processing, null);
        }

        var deliveredCodes = ExtractDeliveredCodes(order);

        if (string.Equals(order.Status, VisoriaOrderStatus.Cancelled, StringComparison.OrdinalIgnoreCase))
        {
            return (SupplierProviderOrderStatus.Failed, deliveredCodes);
        }

        if (string.Equals(order.Status, VisoriaOrderStatus.Completed, StringComparison.OrdinalIgnoreCase))
        {
            var requestedQuantity = (order.Items ?? []).Sum(i => i.Quantity);
            if (requestedQuantity > 0 && deliveredCodes.Count >= requestedQuantity)
            {
                return (SupplierProviderOrderStatus.Succeeded, deliveredCodes);
            }

            return deliveredCodes.Count > 0
                ? (SupplierProviderOrderStatus.PartialFailed, deliveredCodes)
                : (SupplierProviderOrderStatus.Failed, deliveredCodes);
        }

        // Unrecognized/undocumented status string — never guessed.
        return (SupplierProviderOrderStatus.Unknown, null);
    }

    /// <summary>
    /// Only keys actually carrying a value count as delivered — a DELIVERED-status key with no
    /// <c>key</c> value means the API key is missing the <c>keys:read</c> scope, and this integration
    /// cannot treat that as a usable code (see <c>VisoriaOrderKey</c>'s remarks).
    /// </summary>
    private static IReadOnlyCollection<string> ExtractDeliveredCodes(VisoriaOrder order) =>
        (order.Items ?? [])
            .SelectMany(item => item.Keys ?? [])
            .Where(key => string.Equals(key.Status, VisoriaKeyStatus.Delivered, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(key.Key))
            .Select(key => string.IsNullOrEmpty(key.Pin) ? key.Key! : $"{key.Key}:{key.Pin}")
            .ToArray();

    private static SupplierFulfillmentFailureCategory MapFailureCategory(VisoriaApiException ex) => ex.HttpStatusCode switch
    {
        401 or 403 => SupplierFulfillmentFailureCategory.AuthenticationFailed,
        404 => SupplierFulfillmentFailureCategory.InvalidProduct,
        429 => SupplierFulfillmentFailureCategory.ProviderUnavailable,
        // Visoria's documentation does not enumerate a machine-readable code list beyond a handful of
        // examples (AUTHx01/02, REQUESTx01-04, VALIDATORx01, DBx01, ERRORx01) — a 422 validation
        // failure could mean an invalid product id, quantity, face_value, or currency; never guessed
        // into a more specific category than UnknownProviderState without a documented, distinguishable code.
        _ => SupplierFulfillmentFailureCategory.UnknownProviderState,
    };

    /// <summary>Never the raw exception/response — just the documented-safe message; no Authorization header or credential value ever reaches an exception message in this provider.</summary>
    private static string SafeMessage(Exception ex) => ex.Message;
}
