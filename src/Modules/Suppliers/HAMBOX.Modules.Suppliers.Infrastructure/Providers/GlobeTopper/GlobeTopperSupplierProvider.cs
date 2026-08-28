using System.Globalization;
using System.Text.Json;
using HAMBOX.Modules.Suppliers.Application.Abstractions;
using HAMBOX.Modules.Suppliers.Domain.Fulfillments;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace HAMBOX.Modules.Suppliers.Infrastructure.Providers.GlobeTopper;

/// <summary>
/// The third real automated <see cref="ISupplierProvider"/>, following <c>BambooSupplierProvider</c>/
/// <c>VisoriaSupplierProvider</c>'s exact shape. Every GlobeTopper-specific concept (its REST endpoints,
/// Bearer auth, request/response shapes, documented status vocabulary) is contained entirely in this file
/// and the rest of <c>Providers/GlobeTopper/</c> — <see cref="ISupplierFulfillmentService"/> and
/// everything above it never sees any of it, only the generic <see cref="ISupplierProvider"/> surface.
/// </summary>
/// <remarks>
/// <b>Credential mapping</b>: GlobeTopper's documented auth is a single Bearer header whose value is two
/// paired secrets joined by a colon — <c>Authorization: Bearer {{api_key}}:{{api_token}}</c> (the
/// account's login/API key, then its secret/token). This is stored via <c>Supplier.AuthenticationType =
/// ApiKey</c> using the existing <c>ApiKey</c> (= GlobeTopper's key/username) and <c>ApiSecret</c>
/// (= GlobeTopper's secret/token) fields — the same two-paired-value shape <c>BambooSupplierProvider</c>
/// already uses for its Client ID/Client Secret pair — rather than <c>BearerToken</c> (a single
/// already-combined value, which is what that field means for Visoria) or the entity's separate
/// <c>Username</c> field (which nothing in this provider reads). <c>GlobeTopperHttpClient.BuildRequest</c>
/// combines them into the documented header format.
/// </remarks>
internal sealed class GlobeTopperSupplierProvider(GlobeTopperHttpClient httpClient, IMemoryCache cache, ILogger<GlobeTopperSupplierProvider> logger) : ISupplierProvider
{
    public string ProviderType => GlobeTopperProviderConstants.ProviderType;

    // GlobeTopper's Purchase endpoint has no quantity concept at all (see PurchaseAsync's own
    // Quantity != 1 fail-closed check) — one call buys exactly one unit.
    public int? MaxQuantityPerPurchase => 1;

    public async Task<SupplierConnectionTestResult> TestConnectionAsync(SupplierProviderContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var user = await httpClient.GetUserAsync(context.Credentials, cancellationToken);
            return new SupplierConnectionTestResult(true, BuildConnectionSummary(user));
        }
        catch (Exception ex) when (ex is GlobeTopperApiException or GlobeTopperAmbiguousResponseException)
        {
            logger.LogWarning(ex, "GlobeTopper connection test failed for supplier {SupplierId}.", context.SupplierId);
            return new SupplierConnectionTestResult(false, SafeMessage(ex));
        }
    }

    /// <summary>Agent id and account currency only — never <c>available_credit_usd</c>/<c>available_credit_local</c> (financial detail), matching Bamboo/Visoria's identical "no balance in the summary" convention.</summary>
    private static string BuildConnectionSummary(GlobeTopperUser? user) =>
        user is null
            ? "Connected — no account details returned."
            : $"Connected — agent {user.AgentId.ToString(CultureInfo.InvariantCulture)} ({user.Currency?.Code ?? "unknown currency"}).";

    public async Task<SupplierCredentialValidationResult> ValidateCredentialsAsync(SupplierProviderContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await httpClient.GetUserAsync(context.Credentials, cancellationToken);
            return new SupplierCredentialValidationResult(true, null);
        }
        catch (Exception ex) when (ex is GlobeTopperApiException or GlobeTopperAmbiguousResponseException)
        {
            logger.LogWarning(ex, "GlobeTopper credential validation failed for supplier {SupplierId}.", context.SupplierId);
            return new SupplierCredentialValidationResult(false, SafeMessage(ex));
        }
    }

    // Not part of the MVP purchase path — SupplierProductMapping already carries GlobeTopper's operator
    // id per mapping. Honest stub, matching ManualSupplierProvider/BambooSupplierProvider/VisoriaSupplierProvider's identical convention.
    public Task<SupplierProductSyncResult> SyncProductsAsync(SupplierProviderContext context, CancellationToken cancellationToken = default) =>
        Task.FromResult(new SupplierProductSyncResult(false, 0, "GlobeTopper catalog sync is not implemented — map products manually via Supplier Product Mappings."));

    public async Task<SupplierCatalogSearchResult> SearchCatalogAsync(SupplierCatalogQuery query, SupplierProviderContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            // GlobeTopper's product-search endpoint has no free-text search parameter and no pagination
            // at all (confirmed against the live OpenAPI document and a real sandbox call that returned
            // every product in one response) — pulled once and filtered/paged client-side here, the same
            // shape VisoriaSupplierProvider.SearchCatalogAsync already uses for its identical "no
            // per-term lookup" situation. Cached briefly per supplier (search only —
            // GetAvailabilityAsync below always pulls fresh) so the admin UI's per-keystroke search
            // doesn't re-pull the whole catalog on every keystroke.
            var products = await GetCachedProductsAsync(context, cancellationToken);

            IEnumerable<GlobeTopperProduct> filtered = products;
            if (!string.IsNullOrWhiteSpace(query.SearchTerm))
            {
                filtered = filtered.Where(p =>
                    p.Name?.Contains(query.SearchTerm, StringComparison.OrdinalIgnoreCase) == true ||
                    p.Operator?.Name?.Contains(query.SearchTerm, StringComparison.OrdinalIgnoreCase) == true);
            }

            var items = filtered
                .Where(p => p.Operator is not null)
                // Some brands may list more than one denomination record against the same operator id —
                // dedupe by the id that's actually orderable, same defensive GroupBy-First pattern
                // BambooSupplierProvider/VisoriaSupplierProvider use for their own catalog pulls.
                .GroupBy(p => p.Operator!.Id)
                .Select(g => g.First())
                .OrderBy(p => p.Operator!.Id)
                .Skip((Math.Max(1, query.Page) - 1) * Math.Max(1, query.PageSize))
                .Take(Math.Max(1, query.PageSize))
                .Select(ToCatalogItem)
                .ToArray();

            return new SupplierCatalogSearchResult(true, items, null);
        }
        catch (Exception ex) when (ex is GlobeTopperApiException or GlobeTopperAmbiguousResponseException)
        {
            logger.LogWarning(ex, "GlobeTopper catalog search failed for supplier {SupplierId}.", context.SupplierId);
            return new SupplierCatalogSearchResult(false, [], SafeMessage(ex));
        }
    }

    /// <summary>
    /// <see cref="SupplierCatalogItem.Available"/> is unconditionally <see langword="true"/> here — not a
    /// guess: <c>/product/search-all-products</c>'s own documentation states it "retrieves all products
    /// available to the logged in user," so appearing in this result set is itself the documented
    /// availability signal (the same "presence in the endpoint's own result = available" logic
    /// <see cref="GetAvailabilityAsync"/> below reuses for periodic refresh).
    /// </summary>
    private static SupplierCatalogItem ToCatalogItem(GlobeTopperProduct product) => new(
        product.Operator!.Id.ToString(CultureInfo.InvariantCulture),
        product.Name ?? product.Operator.Name ?? "Unknown product",
        product.Operator.Name,
        product.Currency?.Code ?? "USD",
        product.Min,
        product.Max,
        Available: true);

    /// <summary>Bounds how long a pulled catalog is reused across the search box's rapid-fire keystroke requests — never relied on for <see cref="GetAvailabilityAsync"/>'s own correctness, which always pulls fresh (mirrors <c>VisoriaSupplierProvider</c>'s identical split).</summary>
    private static readonly TimeSpan SearchCatalogCacheTtl = TimeSpan.FromSeconds(30);

    private Task<IReadOnlyList<GlobeTopperProduct>> GetCachedProductsAsync(SupplierProviderContext context, CancellationToken cancellationToken)
    {
        var cacheKey = $"globetopper:products:{context.SupplierId}";
        return cache.GetOrCreateAsync(cacheKey, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = SearchCatalogCacheTtl;
            return httpClient.SearchProductsAsync(context.Credentials, cancellationToken);
        })!;
    }

    /// <summary>
    /// Reuses the exact same product-search endpoint <see cref="SearchCatalogAsync"/> calls — GlobeTopper
    /// has no per-id-batch lookup, no stock-quantity field, and no dedicated availability endpoint, so
    /// (per <see cref="ToCatalogItem"/>'s remarks) "present in <c>/product/search-all-products</c>'s
    /// result" is the only documented availability signal available. <see cref="SupplierAvailabilityItem.AvailableQuantity"/>
    /// is always <see langword="null"/> — never fabricated, since no quantity is ever documented.
    /// </summary>
    public async Task<SupplierAvailabilityResult> GetAvailabilityAsync(SupplierAvailabilityQuery query, SupplierProviderContext context, CancellationToken cancellationToken = default)
    {
        var requestedIds = query.ExternalProductIds.ToHashSet(StringComparer.Ordinal);
        if (requestedIds.Count == 0)
        {
            return new SupplierAvailabilityResult(true, [], null);
        }

        IReadOnlyList<GlobeTopperProduct> products;
        try
        {
            products = await httpClient.SearchProductsAsync(context.Credentials, cancellationToken);
        }
        catch (Exception ex) when (ex is GlobeTopperApiException or GlobeTopperAmbiguousResponseException)
        {
            logger.LogWarning(ex, "GlobeTopper availability sync failed for supplier {SupplierId}.", context.SupplierId);
            return new SupplierAvailabilityResult(false, [], SafeMessage(ex));
        }

        var checkedAtUtc = DateTimeOffset.UtcNow;
        var presentIds = products
            .Where(p => p.Operator is not null)
            .Select(p => p.Operator!.Id.ToString(CultureInfo.InvariantCulture))
            .ToHashSet(StringComparer.Ordinal);

        var items = requestedIds
            .Select(id => new SupplierAvailabilityItem(
                id,
                presentIds.Contains(id) ? SupplierAvailabilityState.Available : SupplierAvailabilityState.Unavailable,
                AvailableQuantity: null,
                checkedAtUtc))
            .ToArray();

        return new SupplierAvailabilityResult(true, items, null);
    }

    public Task<SupplierInventorySyncResult> SyncInventoryAsync(SupplierProviderContext context, CancellationToken cancellationToken = default) =>
        Task.FromResult(new SupplierInventorySyncResult(false, 0, "GlobeTopper inventory sync is not implemented — no such endpoint is documented."));

    public Task<SupplierPriceSyncResult> SyncPricesAsync(SupplierProviderContext context, CancellationToken cancellationToken = default) =>
        Task.FromResult(new SupplierPriceSyncResult(false, 0, "GlobeTopper price sync is not implemented — no such endpoint is documented."));

    // GlobeTopper's documented API has no reservation step — Purchase buys directly.
    public Task<SupplierReservationResult> ReserveAsync(SupplierReservationRequest request, SupplierProviderContext context, CancellationToken cancellationToken = default) =>
        Task.FromResult(new SupplierReservationResult(false, null, "GlobeTopper does not support reservations — purchase directly."));

    // No cancellation/refund endpoint is documented anywhere in the OpenAPI document. Reporting
    // unsupported rather than inventing one, matching Bamboo/Visoria's identical honest-stub choice.
    public Task<SupplierCancellationResult> CancelAsync(SupplierCancellationRequest request, SupplierProviderContext context, CancellationToken cancellationToken = default) =>
        Task.FromResult(new SupplierCancellationResult(false, "GlobeTopper does not document a cancellation/refund API."));

    public async Task<SupplierPurchaseResult> PurchaseAsync(SupplierPurchaseRequest request, SupplierProviderContext context, CancellationToken cancellationToken = default)
    {
        // GlobeTopper's Purchase endpoint (POST /transaction/do-by-product/{productID}/{amount}) has no
        // quantity concept anywhere in its documented request shape (path segments are the product id and
        // a single face-value amount; the only documented body fields are email/first_name/last_name/
        // order_id) — one call buys exactly one unit. Rather than guessing an undocumented multi-call
        // composition (with its own undocumented partial-failure semantics), quantity > 1 fails closed.
        if (request.Quantity != 1)
        {
            return new SupplierPurchaseResult(false, null, null, SupplierFulfillmentFailureCategory.InvalidConfiguration,
                "GlobeTopper's purchase endpoint supports exactly one unit per call — quantity greater than 1 is not supported by this integration.");
        }

        if (!long.TryParse(request.ExternalProductId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var productId))
        {
            return new SupplierPurchaseResult(false, null, null, SupplierFulfillmentFailureCategory.InvalidConfiguration,
                "The supplier product mapping's external product id is not a valid GlobeTopper numeric operator id.");
        }

        if (request.UnitFaceValue is not decimal amount)
        {
            return new SupplierPurchaseResult(false, null, null, SupplierFulfillmentFailureCategory.InvalidConfiguration,
                "No face value configured on the supplier product mapping (BuyingPrice) — required as GlobeTopper's {amount} path parameter.");
        }

        if (!Guid.TryParse(request.ReferenceId, out var referenceGuid))
        {
            return new SupplierPurchaseResult(false, null, null, SupplierFulfillmentFailureCategory.InvalidConfiguration,
                "ReferenceId was not a valid GUID — cannot derive a GlobeTopper order_id.");
        }

        // No try/catch around GlobeTopperAmbiguousResponseException here — it (and any other unexpected
        // exception) propagates to the caller by design, per ISupplierProvider.PurchaseAsync's documented
        // ambiguity contract: an exception here means "unknown, resolve via GetOrderStatusAsync" — though
        // see that method's remarks for GlobeTopper's genuine limitation there.
        GlobeTopperEnvelope<GlobeTopperTransaction> envelope;
        try
        {
            envelope = await httpClient.PurchaseAsync(context.Credentials, productId, amount, DeriveOrderId(referenceGuid), cancellationToken);
        }
        catch (GlobeTopperApiException ex)
        {
            logger.LogWarning(
                "GlobeTopper purchase definitively rejected for HamboxReferenceId {HamboxReferenceId}: HTTP {StatusCode}.",
                referenceGuid, ex.HttpStatusCode);
            return new SupplierPurchaseResult(false, null, null, MapHttpFailureCategory(ex), SafeMessage(ex));
        }

        return ApplyPurchaseEnvelope(referenceGuid, envelope);
    }

    /// <summary>
    /// GlobeTopper's <c>order_id</c> is a required <c>int64</c> field — <c>SupplierFulfillment.HamboxReferenceId</c>
    /// is a GUID and does not fit. Derived deterministically (same reference always produces the same
    /// value) so a retry is at least traceable to the same GlobeTopper-side order_id, though — per this
    /// provider's Idempotency remarks in the README — GlobeTopper does not document this field as an
    /// actual dedup key, so this is a best-effort traceability aid, not a safety guarantee.
    /// </summary>
    private static long DeriveOrderId(Guid referenceId)
    {
        var value = BitConverter.ToInt64(referenceId.ToByteArray(), 0);
        return value == long.MinValue ? long.MaxValue : Math.Abs(value);
    }

    private SupplierPurchaseResult ApplyPurchaseEnvelope(Guid hamboxReferenceId, GlobeTopperEnvelope<GlobeTopperTransaction> envelope)
    {
        if (envelope.ResponseCode != GlobeTopperResponseCode.Success)
        {
            logger.LogWarning(
                "GlobeTopper purchase reported a business failure for HamboxReferenceId {HamboxReferenceId}: responseCode {ResponseCode}.",
                hamboxReferenceId, envelope.ResponseCode);
            return new SupplierPurchaseResult(false, null, null, MapResponseCodeFailureCategory(envelope.ResponseCode), envelope.ResponseMessage);
        }

        var transaction = envelope.Records?.FirstOrDefault();
        if (transaction is null)
        {
            // Malformed: a documented success (responseCode 200) with nothing to track it by. Cannot trust it.
            throw new GlobeTopperAmbiguousResponseException("GlobeTopper reported responseCode 200 with no transaction record — cannot confirm the purchase.");
        }

        var deliveredCodes = ExtractDeliveredCodes(transaction);
        if (deliveredCodes.Count == 0)
        {
            // Same "never trust an empty success" rule Bamboo applies to an empty requestId — a
            // responseCode-200 transaction with nothing in extra_fields is inconsistent, not a real success.
            throw new GlobeTopperAmbiguousResponseException("GlobeTopper reported a successful transaction with no delivered redemption data (extra_fields).");
        }

        return new SupplierPurchaseResult(true, transaction.TransId.ToString(CultureInfo.InvariantCulture), deliveredCodes, null, null);
    }

    public async Task<SupplierOrderStatusResult> GetOrderStatusAsync(SupplierOrderStatusQuery query, SupplierProviderContext context, CancellationToken cancellationToken = default)
    {
        // Genuine, documented limitation: GlobeTopper's only transaction lookup is by its own trans_id
        // (GET /transaction/search-transactions/{transactionID}) — there is no lookup by any
        // client-supplied reference (no idempotency key, no order_id search filter; the list endpoint's
        // only filters are startDate/endDate/status/msisdn). Unlike Bamboo (looks up by its own RequestId
        // = HamboxReferenceId) and Visoria (looks up by its own Idempotency-Key = HamboxReferenceId), a
        // fulfillment whose purchase call was ambiguous BEFORE a trans_id was ever captured cannot be
        // reconciled by this provider at all — this is intentionally NOT worked around (there is nothing
        // documented to work around it with); it surfaces as a permanently ambiguous
        // (SupplierFulfillmentStatus.Unknown) attempt requiring manual reconciliation via GlobeTopper's
        // own support/portal. See docs/integrations/suppliers/README.md for the full explanation.
        if (string.IsNullOrWhiteSpace(query.ProviderOrderId))
        {
            throw new GlobeTopperAmbiguousResponseException(
                "GlobeTopper has no transaction lookup by client reference — without a captured trans_id (ProviderOrderId), this fulfillment's outcome cannot be queried. Manual reconciliation via GlobeTopper support is required.");
        }

        // No try/catch: any failure here is caught by the orchestrator's ReconcileAsync, which treats it
        // as "still can't resolve, try again later" regardless of the specific cause.
        var transaction = await httpClient.GetTransactionAsync(context.Credentials, query.ProviderOrderId, cancellationToken);
        if (transaction is null)
        {
            throw new GlobeTopperAmbiguousResponseException($"GlobeTopper returned no transaction for trans_id '{query.ProviderOrderId}'.");
        }

        var status = MapTransactionStatus(transaction.StatusDescription);
        if (status == SupplierProviderOrderStatus.Succeeded)
        {
            var deliveredCodes = ExtractDeliveredCodes(transaction);
            if (deliveredCodes.Count == 0)
            {
                // Same "never trust an inconsistent success" rule as the synchronous purchase path.
                return new SupplierOrderStatusResult(SupplierProviderOrderStatus.Failed, transaction.TransId.ToString(CultureInfo.InvariantCulture), [],
                    SupplierFulfillmentFailureCategory.UnknownProviderState, "GlobeTopper reported a successful transaction with no delivered redemption data (extra_fields).");
            }

            return new SupplierOrderStatusResult(SupplierProviderOrderStatus.Succeeded, transaction.TransId.ToString(CultureInfo.InvariantCulture), deliveredCodes, null, null);
        }

        if (status == SupplierProviderOrderStatus.Failed)
        {
            return new SupplierOrderStatusResult(SupplierProviderOrderStatus.Failed, transaction.TransId.ToString(CultureInfo.InvariantCulture), [],
                SupplierFulfillmentFailureCategory.UnknownProviderState, transaction.StatusDescription);
        }

        // Unrecognized status_description — never guessed. GlobeTopper's documentation never shows a
        // real example of a failed transaction's status_description (only a successful one, and the
        // query-filter enum's own "0 = Failed" claim directly contradicts that successful example's
        // status field — see the README) — staying Unknown here is the only honest choice.
        return new SupplierOrderStatusResult(SupplierProviderOrderStatus.Unknown, transaction.TransId.ToString(CultureInfo.InvariantCulture), null, null,
            $"Unrecognized GlobeTopper status_description: '{transaction.StatusDescription}'.");
    }

    /// <summary>
    /// Only <see cref="GlobeTopperStatusDescription.Success"/> is confirmed from a real example. Mapping
    /// the literal string <see cref="GlobeTopperStatusDescription.Failed"/> to a definite failure is an
    /// inference (GlobeTopper's own query-filter documentation for <c>/transaction/search-transactions</c>
    /// uses exactly that vocabulary — "0 is Failed, 1 is Success" — even though no real failed-transaction
    /// example body was ever observed) — not a wild guess, but flagged as unconfirmed in the README. Any
    /// other value stays <see cref="SupplierProviderOrderStatus.Unknown"/>.
    /// </summary>
    private static SupplierProviderOrderStatus MapTransactionStatus(string? statusDescription) => statusDescription switch
    {
        GlobeTopperStatusDescription.Success => SupplierProviderOrderStatus.Succeeded,
        GlobeTopperStatusDescription.Failed => SupplierProviderOrderStatus.Failed,
        _ => SupplierProviderOrderStatus.Unknown,
    };

    /// <summary>
    /// GlobeTopper has no single fixed "the code" field — <c>extra_fields</c> varies per product (Pin
    /// Number, Claim Code, Redemption URL, Barcode Number + Barcode URL, Security Code, ...). Every
    /// non-empty entry is preserved as a <c>"Label: Value"</c> line rather than guessing which one is
    /// authoritative, joined into ONE opaque string (since GlobeTopper's purchase is always exactly one
    /// unit — see <see cref="PurchaseAsync"/>'s remarks — this always yields a single-element collection,
    /// matching <c>RequestedQuantity</c>). Array-valued fields are joined with a comma; only string,
    /// number, and array-of-primitive values are handled — anything else is skipped rather than guessed.
    /// </summary>
    private static IReadOnlyCollection<string> ExtractDeliveredCodes(GlobeTopperTransaction transaction)
    {
        if (transaction.ExtraFields is null || transaction.ExtraFields.Count == 0)
        {
            return [];
        }

        var parts = new List<string>();
        foreach (var (key, value) in transaction.ExtraFields)
        {
            var text = value.ValueKind switch
            {
                JsonValueKind.String => value.GetString(),
                JsonValueKind.Number => value.ToString(),
                JsonValueKind.Array => string.Join(", ", value.EnumerateArray()
                    .Where(e => e.ValueKind is JsonValueKind.String or JsonValueKind.Number)
                    .Select(e => e.ToString())),
                _ => null,
            };

            if (!string.IsNullOrWhiteSpace(text))
            {
                parts.Add($"{key}: {text}");
            }
        }

        return parts.Count == 0 ? [] : [string.Join("; ", parts)];
    }

    private static SupplierFulfillmentFailureCategory MapHttpFailureCategory(GlobeTopperApiException ex) => ex.HttpStatusCode switch
    {
        401 or 403 => SupplierFulfillmentFailureCategory.AuthenticationFailed,
        429 => SupplierFulfillmentFailureCategory.ProviderUnavailable,
        _ => SupplierFulfillmentFailureCategory.UnknownProviderState,
    };

    private static SupplierFulfillmentFailureCategory MapResponseCodeFailureCategory(int? responseCode) => responseCode switch
    {
        GlobeTopperResponseCode.OutOfStock or GlobeTopperResponseCode.ProductUnavailable => SupplierFulfillmentFailureCategory.ProductUnavailable,
        GlobeTopperResponseCode.AccountBalanceInsufficientMaster or GlobeTopperResponseCode.AccountBalanceInsufficient => SupplierFulfillmentFailureCategory.InsufficientSupplierBalance,
        GlobeTopperResponseCode.AccessDeniedAccountBlocked or GlobeTopperResponseCode.AccessDeniedInvalidIp => SupplierFulfillmentFailureCategory.AuthenticationFailed,
        // InternalError (0) and TransactionFailed (2) are documented but carry no more specific meaning
        // than "it failed" — never guessed into a more specific category. Any other undocumented code
        // falls through to the same safe default.
        _ => SupplierFulfillmentFailureCategory.UnknownProviderState,
    };

    /// <summary>Never the raw exception/response — just the documented-safe message; no Authorization header or credential value ever reaches an exception message in this provider.</summary>
    private static string SafeMessage(Exception ex) => ex.Message;
}
