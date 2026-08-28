using HAMBOX.Modules.Suppliers.Application.Abstractions;
using HAMBOX.Modules.Suppliers.Domain.Fulfillments;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace HAMBOX.Modules.Suppliers.Infrastructure.Providers.CodesWholesale;

/// <summary>
/// The fifth real automated <see cref="ISupplierProvider"/>, following <c>BambooSupplierProvider</c>/
/// <c>VisoriaSupplierProvider</c>/<c>GlobeTopperSupplierProvider</c>/<c>EnebaSupplierProvider</c>'s exact
/// shape. Every CodesWholesale-specific concept (its v2 REST endpoints, OAuth2 client-credentials auth,
/// request/response shapes, documented code-status vocabulary) is contained entirely in this file and
/// the rest of <c>Providers/CodesWholesale/</c> — <see cref="ISupplierFulfillmentService"/> and
/// everything above it never sees any of it, only the generic <see cref="ISupplierProvider"/> surface.
/// </summary>
/// <remarks>
/// <b>Credential mapping</b>: <c>Supplier.AuthenticationType = ApiKey</c>, with <c>ApiKey</c> = Client ID
/// and <c>ApiSecret</c> = Client Secret — the same two-paired-value shape <c>GlobeTopperSupplierProvider</c>
/// already uses, rather than introducing an <c>OAuthSettingsJson</c> blob (<c>EnebaSupplierProvider</c>'s
/// shape) for what is, for CodesWholesale, only ever two plain values with no extra metadata.
/// </remarks>
internal sealed class CodesWholesaleSupplierProvider(CodesWholesaleHttpClient httpClient, IMemoryCache cache, ILogger<CodesWholesaleSupplierProvider> logger) : ISupplierProvider
{
    public string ProviderType => CodesWholesaleProviderConstants.ProviderType;

    // No cap is documented anywhere in the CodesWholesale API — each order-line quantity is unbounded per the SDK.
    public int? MaxQuantityPerPurchase => null;

    public async Task<SupplierConnectionTestResult> TestConnectionAsync(SupplierProviderContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var account = await httpClient.GetAccountAsync(context, cancellationToken);
            return new SupplierConnectionTestResult(true, BuildConnectionSummary(account));
        }
        catch (Exception ex) when (ex is CodesWholesaleApiException or CodesWholesaleAmbiguousResponseException)
        {
            logger.LogWarning(ex, "CodesWholesale connection test failed for supplier {SupplierId}.", context.SupplierId);
            return new SupplierConnectionTestResult(false, SafeMessage(ex));
        }
    }

    /// <summary>Full name/email only — never <c>currentBalance</c>/<c>currentCredit</c>/<c>totalToUse</c> (financial detail), matching Bamboo/GlobeTopper's identical "no balance in the summary" convention.</summary>
    private static string BuildConnectionSummary(CodesWholesaleAccount account) =>
        string.IsNullOrWhiteSpace(account.Email)
            ? "Connected — no account details returned."
            : $"Connected — account {account.Email}.";

    public async Task<SupplierCredentialValidationResult> ValidateCredentialsAsync(SupplierProviderContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await httpClient.GetAccountAsync(context, cancellationToken);
            return new SupplierCredentialValidationResult(true, null);
        }
        catch (Exception ex) when (ex is CodesWholesaleApiException or CodesWholesaleAmbiguousResponseException)
        {
            logger.LogWarning(ex, "CodesWholesale credential validation failed for supplier {SupplierId}.", context.SupplierId);
            return new SupplierCredentialValidationResult(false, SafeMessage(ex));
        }
    }

    // Not part of the MVP purchase path — SupplierProductMapping already carries CodesWholesale's
    // productId per mapping. Honest stub, matching every other real provider's identical convention.
    public Task<SupplierProductSyncResult> SyncProductsAsync(SupplierProviderContext context, CancellationToken cancellationToken = default) =>
        Task.FromResult(new SupplierProductSyncResult(false, 0, "CodesWholesale catalog sync is not implemented — map products manually via Supplier Product Mappings."));

    public async Task<SupplierCatalogSearchResult> SearchCatalogAsync(SupplierCatalogQuery query, SupplierProviderContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            // CodesWholesale's /v2/products has no free-text search parameter and no server-side
            // pagination (confirmed: AbstractCollectionResource's "items" field carries the whole
            // response, with no page/limit metadata ever populated from the wire) — pulled once and
            // filtered/paged client-side here, the same shape GlobeTopperSupplierProvider/VisoriaSupplierProvider
            // already use for their identical "no per-term lookup" situation. Cached briefly per
            // supplier (search only — GetAvailabilityAsync below always pulls fresh).
            var products = await GetCachedProductsAsync(context, cancellationToken);

            IEnumerable<CodesWholesaleProduct> filtered = products;
            if (!string.IsNullOrWhiteSpace(query.SearchTerm))
            {
                filtered = filtered.Where(p =>
                    p.Name?.Contains(query.SearchTerm, StringComparison.OrdinalIgnoreCase) == true ||
                    p.Identifier?.Contains(query.SearchTerm, StringComparison.OrdinalIgnoreCase) == true);
            }

            var items = filtered
                .Where(p => !string.IsNullOrWhiteSpace(p.ProductId))
                .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
                .Skip((Math.Max(1, query.Page) - 1) * Math.Max(1, query.PageSize))
                .Take(Math.Max(1, query.PageSize))
                .Select(ToCatalogItem)
                .ToArray();

            return new SupplierCatalogSearchResult(true, items, null);
        }
        catch (Exception ex) when (ex is CodesWholesaleApiException or CodesWholesaleAmbiguousResponseException)
        {
            logger.LogWarning(ex, "CodesWholesale catalog search failed for supplier {SupplierId}.", context.SupplierId);
            return new SupplierCatalogSearchResult(false, [], SafeMessage(ex));
        }
    }

    /// <summary><see cref="SupplierCatalogItem.Available"/> reflects the real <c>quantity</c> stock count — unlike GlobeTopper/Bamboo, CodesWholesale documents an actual numeric stock field (confirmed: <c>Product::getStockQuantity</c>), so "available" here means <c>quantity &gt; 0</c>, not just "present in the price list".</summary>
    private static SupplierCatalogItem ToCatalogItem(CodesWholesaleProduct product)
    {
        var (min, max) = PriceRange(product.Prices);
        return new SupplierCatalogItem(
            product.ProductId!,
            product.Name ?? product.Identifier ?? "Unknown product",
            product.Platform,
            "USD",
            min,
            max,
            Available: product.Quantity is null or > 0);
    }

    /// <summary>Lowest/highest quantity-tier <c>price</c> values (confirmed real quantity-based pricing — <c>Resource/Price.php</c>'s <c>from</c>/<c>to</c> range fields) — never a guessed single price.</summary>
    private static (decimal? Min, decimal? Max) PriceRange(IReadOnlyList<CodesWholesalePrice>? prices)
    {
        if (prices is not { Count: > 0 })
        {
            return (null, null);
        }

        var values = prices.Select(p => p.Value).ToArray();
        return (values.Min(), values.Max());
    }

    /// <summary>Bounds how long a pulled catalog is reused across the search box's rapid-fire keystroke requests — never relied on for <see cref="GetAvailabilityAsync"/>'s own correctness, which always pulls fresh (mirrors <c>GlobeTopperSupplierProvider</c>'s identical split).</summary>
    private static readonly TimeSpan SearchCatalogCacheTtl = TimeSpan.FromSeconds(30);

    private Task<IReadOnlyList<CodesWholesaleProduct>> GetCachedProductsAsync(SupplierProviderContext context, CancellationToken cancellationToken)
    {
        var cacheKey = $"codeswholesale:products:{context.SupplierId}";
        return cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = SearchCatalogCacheTtl;
            var response = await httpClient.GetProductsAsync(context, productIds: null, cancellationToken);
            return response.Items ?? [];
        })!;
    }

    /// <summary>Bounded so a mapping set with unexpectedly many external ids can never turn one sync tick into an unbounded number of HTTP calls or one unreasonably long query string.</summary>
    private const int AvailabilityBatchSize = 100;

    public async Task<SupplierAvailabilityResult> GetAvailabilityAsync(SupplierAvailabilityQuery query, SupplierProviderContext context, CancellationToken cancellationToken = default)
    {
        var requestedIds = query.ExternalProductIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).ToArray();
        if (requestedIds.Length == 0)
        {
            return new SupplierAvailabilityResult(true, [], null);
        }

        var found = new Dictionary<string, CodesWholesaleProduct>(StringComparer.Ordinal);
        try
        {
            foreach (var batch in requestedIds.Chunk(AvailabilityBatchSize))
            {
                var response = await httpClient.GetProductsAsync(context, batch, cancellationToken);
                foreach (var product in response.Items ?? [])
                {
                    if (!string.IsNullOrWhiteSpace(product.ProductId))
                    {
                        found[product.ProductId] = product;
                    }
                }
            }
        }
        catch (Exception ex) when (ex is CodesWholesaleApiException or CodesWholesaleAmbiguousResponseException)
        {
            logger.LogWarning(ex, "CodesWholesale availability sync failed for supplier {SupplierId}.", context.SupplierId);
            return new SupplierAvailabilityResult(false, [], SafeMessage(ex));
        }

        var checkedAtUtc = DateTimeOffset.UtcNow;
        var items = requestedIds
            .Select(id => found.TryGetValue(id, out var product)
                ? new SupplierAvailabilityItem(id, product.Quantity is null or > 0 ? SupplierAvailabilityState.Available : SupplierAvailabilityState.Unavailable, product.Quantity, checkedAtUtc)
                // Not present in the price list at all — CodesWholesale has no per-id lookup to fall
                // back to, so per the provider contract this is a definite "not offered right now".
                : new SupplierAvailabilityItem(id, SupplierAvailabilityState.Unavailable, null, checkedAtUtc))
            .ToArray();

        return new SupplierAvailabilityResult(true, items, null);
    }

    public Task<SupplierInventorySyncResult> SyncInventoryAsync(SupplierProviderContext context, CancellationToken cancellationToken = default) =>
        Task.FromResult(new SupplierInventorySyncResult(false, 0, "CodesWholesale inventory sync is not implemented — use GetAvailabilityAsync's periodic sync instead."));

    public Task<SupplierPriceSyncResult> SyncPricesAsync(SupplierProviderContext context, CancellationToken cancellationToken = default) =>
        Task.FromResult(new SupplierPriceSyncResult(false, 0, "CodesWholesale price sync is not implemented — no bulk price-sync endpoint is documented beyond the catalog's own quantity-tier prices."));

    // CodesWholesale's documented API has no reservation step — Create Order purchases directly.
    public Task<SupplierReservationResult> ReserveAsync(SupplierReservationRequest request, SupplierProviderContext context, CancellationToken cancellationToken = default) =>
        Task.FromResult(new SupplierReservationResult(false, null, "CodesWholesale does not support reservations — purchase directly."));

    // No cancel/refund endpoint is documented anywhere in the available SDK sources — the public FAQ
    // describes only CodesWholesale's own automatic pre-order cancellation, never a client-callable API.
    public Task<SupplierCancellationResult> CancelAsync(SupplierCancellationRequest request, SupplierProviderContext context, CancellationToken cancellationToken = default) =>
        Task.FromResult(new SupplierCancellationResult(false, "CodesWholesale does not document a client-callable cancellation/refund API."));

    public async Task<SupplierPurchaseResult> PurchaseAsync(SupplierPurchaseRequest request, SupplierProviderContext context, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.ExternalProductId))
        {
            return new SupplierPurchaseResult(false, null, null, SupplierFulfillmentFailureCategory.InvalidConfiguration,
                "No CodesWholesale productId configured on the supplier product mapping.");
        }

        if (string.IsNullOrWhiteSpace(request.ReferenceId))
        {
            return new SupplierPurchaseResult(false, null, null, SupplierFulfillmentFailureCategory.InvalidConfiguration,
                "ReferenceId is required — CodesWholesale's orderId idempotency field cannot be sent empty.");
        }

        var orderRequest = new CodesWholesaleOrderRequest
        {
            Products = [new CodesWholesaleOrderProductEntry { ProductId = request.ExternalProductId, Quantity = request.Quantity }],
            OrderId = request.ReferenceId,
            AllowPreOrder = CodesWholesaleHttpClient.ResolveAllowPreOrder(context),
        };

        // No try/catch around CodesWholesaleAmbiguousResponseException here — it (and any other
        // unexpected exception) propagates to the caller by design, per ISupplierProvider.PurchaseAsync's
        // documented ambiguity contract: an exception here means "unknown, resolve via GetOrderStatusAsync".
        CodesWholesaleOrder order;
        try
        {
            order = await httpClient.CreateOrderAsync(context, orderRequest, cancellationToken);
        }
        catch (CodesWholesaleApiException ex)
        {
            logger.LogWarning(
                "CodesWholesale purchase definitively rejected for HamboxReferenceId {HamboxReferenceId}: HTTP {StatusCode} (errorCode {ErrorCode}).",
                request.ReferenceId, ex.HttpStatusCode, ex.ErrorCode);
            return new SupplierPurchaseResult(false, null, null, MapFailureCategory(ex), SafeMessage(ex));
        }

        if (string.IsNullOrWhiteSpace(order.OrderId))
        {
            // Malformed: an apparent success with nothing to track it by. Cannot trust it.
            throw new CodesWholesaleAmbiguousResponseException("CodesWholesale accepted the order but returned no orderId — cannot confirm or later reconcile it.");
        }

        var outcome = await ResolveOrderOutcomeAsync(context, order, cancellationToken);
        return outcome.AllDelivered
            ? new SupplierPurchaseResult(true, order.OrderId, outcome.DeliveredCodes, null, null)
            // Some/all codes are still "Pre-ordered code" — accepted, outcome not yet fully known.
            // DeliveredCodes stays null, driving the orchestrator's Submitted state (not Succeeded)
            // until GetOrderStatusAsync confirms the remainder.
            : new SupplierPurchaseResult(true, order.OrderId, DeliveredCodes: null, FailureCategory: null, Message: null);
    }

    public async Task<SupplierOrderStatusResult> GetOrderStatusAsync(SupplierOrderStatusQuery query, SupplierProviderContext context, CancellationToken cancellationToken = default)
    {
        CodesWholesaleOrder order;

        if (!string.IsNullOrWhiteSpace(query.ProviderOrderId))
        {
            // No try/catch: any failure here is caught by the orchestrator's ReconcileAsync, which treats
            // it as "still can't resolve, try again later" regardless of the specific cause.
            order = await httpClient.GetOrderAsync(context, query.ProviderOrderId, cancellationToken);
        }
        else
        {
            // Genuine recovery path for a purchase whose CreateOrder call was itself ambiguous before an
            // orderId was ever captured. Unlike GlobeTopper/Eneba (no lookup by client reference at
            // all — a permanent dead end), CodesWholesale's order-history endpoint
            // (GET /v2/orders?startFrom=&endOn=) DOES return each order's clientOrderId (confirmed:
            // Order::getClientOrderId(), used by OrderList's own history example) — so history can be
            // searched for the matching HamboxReferenceId. This is a real, SDK-confirmed mechanism, not
            // invented, though it is still a fallback: CodesWholesale documents no server-side filter by
            // clientOrderId, so this pulls a bounded date-range window and matches client-side.
            var found = await FindOrderByClientReferenceAsync(context, query.HamboxReferenceId.ToString(), cancellationToken);
            if (found is null)
            {
                throw new CodesWholesaleAmbiguousResponseException(
                    $"CodesWholesale has no orderId on file for this attempt, and no order with clientOrderId '{query.HamboxReferenceId}' was found in the last {ReconciliationLookbackDaysHint} days of order history — manual reconciliation via the CodesWholesale dashboard is required.");
            }

            order = found;
        }

        var outcome = await ResolveOrderOutcomeAsync(context, order, cancellationToken);
        if (outcome.AllDelivered)
        {
            return new SupplierOrderStatusResult(SupplierProviderOrderStatus.Succeeded, order.OrderId, outcome.DeliveredCodes, null, order.Status);
        }

        // Still has at least one "Pre-ordered code" entry — not yet a terminal outcome (CodesWholesale's
        // documented pre-order assignment window is up to 14 days). Never guessed into Failed: the
        // available sources never showed a confirmed example of a failed/cancelled order-status value.
        return new SupplierOrderStatusResult(SupplierProviderOrderStatus.Processing, order.OrderId, null, null, order.Status);
    }

    // Kept as a constant string for the exception message above rather than threading CodesWholesaleProviderOptions
    // through every call site just for one diagnostic number.
    private const int ReconciliationLookbackDaysHint = 7;

    private async Task<CodesWholesaleOrder?> FindOrderByClientReferenceAsync(SupplierProviderContext context, string hamboxReferenceId, CancellationToken cancellationToken)
    {
        var endOn = DateOnly.FromDateTime(DateTime.UtcNow);
        var startFrom = endOn.AddDays(-ReconciliationLookbackDaysHint);

        var history = await httpClient.GetOrderHistoryAsync(context, startFrom, endOn, cancellationToken);
        return (history.Items ?? []).FirstOrDefault(o => string.Equals(o.ClientOrderId, hamboxReferenceId, StringComparison.Ordinal));
    }

    private sealed record OrderOutcome(bool AllDelivered, IReadOnlyCollection<string>? DeliveredCodes);

    /// <summary>
    /// Shared success/pending determination for both <see cref="PurchaseAsync"/> (synchronous CreateOrder
    /// response) and <see cref="GetOrderStatusAsync"/> (a later GetOrder/history lookup) — CodesWholesale
    /// uses the exact same code-entry shape in both responses, so the decision logic must never drift
    /// between the two call sites. A code whose inline <c>code</c> value is empty is looked up
    /// individually via <c>GET /v2/codes/{codeId}</c> — mirrors the official PHP SDK's own
    /// <c>Code::getCode()</c> lazy-refetch behavior (only re-fetches when the inline value is blank),
    /// rather than always issuing one extra call per code regardless of whether it's needed.
    /// </summary>
    /// <remarks>
    /// Throws <see cref="CodesWholesaleAmbiguousResponseException"/> (never a definite failure) when: the
    /// order has no code entries at all (a documented "success" with nothing to trust), a delivered text
    /// code's value is empty even after the direct lookup, or any code is <c>"Image code"</c> — HAMBOX's
    /// <c>OrderLicenseKey</c> pipeline stores only plain-text redemption keys, and there is no safe way to
    /// represent an image-format code without either discarding a real, paid-for purchase (reporting
    /// Failed would risk a duplicate re-purchase elsewhere) or fabricating a useless text value. Manual
    /// reconciliation via the CodesWholesale dashboard is the only safe path for that product; do not map
    /// an image-delivered CodesWholesale product to a HAMBOX variant.
    /// </remarks>
    private async Task<OrderOutcome> ResolveOrderOutcomeAsync(SupplierProviderContext context, CodesWholesaleOrder order, CancellationToken cancellationToken)
    {
        var entries = (order.Products ?? []).SelectMany(p => p.Codes ?? []).ToArray();
        if (entries.Length == 0)
        {
            throw new CodesWholesaleAmbiguousResponseException($"CodesWholesale order {order.OrderId} has no code entries — cannot confirm the purchase.");
        }

        var resolvedCodes = new List<string>();
        var allDelivered = true;

        foreach (var entry in entries)
        {
            if (string.Equals(entry.Status, CodesWholesaleProviderConstants.CodeStatusPreOrder, StringComparison.Ordinal))
            {
                allDelivered = false;
                continue;
            }

            if (string.Equals(entry.Status, CodesWholesaleProviderConstants.CodeStatusImage, StringComparison.Ordinal))
            {
                throw new CodesWholesaleAmbiguousResponseException(
                    $"CodesWholesale order {order.OrderId} delivered an image-format code (codeId {entry.CodeId}), which this integration cannot store as a text license key — manual reconciliation required. Do not map this product for automated fulfillment.");
            }

            if (!string.Equals(entry.Status, CodesWholesaleProviderConstants.CodeStatusText, StringComparison.Ordinal))
            {
                // Unrecognized status string — never guessed as delivered. Stays pending; a later
                // reconciliation attempt re-checks it.
                logger.LogWarning("CodesWholesale order {OrderId} returned an unrecognized code status '{Status}' for codeId {CodeId}.", order.OrderId, entry.Status, entry.CodeId);
                allDelivered = false;
                continue;
            }

            var codeValue = entry.Code;
            if (string.IsNullOrWhiteSpace(codeValue) && !string.IsNullOrWhiteSpace(entry.CodeId))
            {
                var fetched = await httpClient.GetCodeAsync(context, entry.CodeId, cancellationToken);
                codeValue = fetched.Code;
            }

            if (string.IsNullOrWhiteSpace(codeValue))
            {
                throw new CodesWholesaleAmbiguousResponseException(
                    $"CodesWholesale reported a delivered text code (codeId {entry.CodeId}) with no code value, even after a direct lookup.");
            }

            resolvedCodes.Add(codeValue);
        }

        return new OrderOutcome(allDelivered, allDelivered ? resolvedCodes : null);
    }

    private static SupplierFulfillmentFailureCategory MapFailureCategory(CodesWholesaleApiException ex)
    {
        if (ex.HttpStatusCode is 401 or 403)
        {
            return SupplierFulfillmentFailureCategory.AuthenticationFailed;
        }

        if (ex.HttpStatusCode == 429)
        {
            return SupplierFulfillmentFailureCategory.ProviderUnavailable;
        }

        return ex.ErrorCode switch
        {
            CodesWholesaleProviderConstants.ErrorCodeInsufficientBalance => SupplierFulfillmentFailureCategory.InsufficientSupplierBalance,
            CodesWholesaleProviderConstants.ErrorCodeProductNotFound => SupplierFulfillmentFailureCategory.InvalidProduct,
            // Every other documented/undocumented business error code stays UnknownProviderState —
            // only the two confirmed in examples/create-order.php are mapped more specifically.
            _ => SupplierFulfillmentFailureCategory.UnknownProviderState,
        };
    }

    /// <summary>Never the raw exception/response — just the documented-safe message; no Client Secret or access token ever reaches an exception message in this provider.</summary>
    private static string SafeMessage(Exception ex) => ex.Message;
}
