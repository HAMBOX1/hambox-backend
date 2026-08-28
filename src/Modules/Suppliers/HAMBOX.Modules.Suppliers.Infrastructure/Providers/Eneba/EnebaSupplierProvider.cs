using HAMBOX.Modules.Suppliers.Application.Abstractions;
using HAMBOX.Modules.Suppliers.Domain.Fulfillments;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HAMBOX.Modules.Suppliers.Infrastructure.Providers.Eneba;

/// <summary>
/// The fourth real automated <see cref="ISupplierProvider"/>, following <c>BambooSupplierProvider</c>/
/// <c>VisoriaSupplierProvider</c>/<c>GlobeTopperSupplierProvider</c>'s exact shape. Every Eneba-specific
/// concept (GraphQL, OAuth2 client-credentials auth, wholesale-auction purchasing, the encrypted
/// key-export archive) is contained entirely in this file and the rest of <c>Providers/Eneba/</c> —
/// <see cref="ISupplierFulfillmentService"/> and everything above it never sees any of it, only the
/// generic <see cref="ISupplierProvider"/> surface.
/// </summary>
/// <remarks>
/// <b>Credential mapping</b>: Eneba's documented auth is OAuth2 client-credentials (Auth ID + Auth
/// Secret) — the first provider in this codebase to actually use <c>Supplier.AuthenticationType =
/// OAuth2</c> / <c>Supplier.OAuthSettingsJson</c> (previously reserved but unused). The JSON blob also
/// carries <c>accountEmail</c> — NOT an API credential, but required to decrypt the key-export archive
/// (see <see cref="EnebaArchiveReader"/>) — see <see cref="EnebaOAuthSettings"/>'s remarks for why it
/// lives here rather than the non-secret <c>Supplier.SettingsJson</c>.
///
/// <b>Prerequisite this integration cannot verify or work around</b>: purchasing requires the Eneba
/// account behind the configured Auth ID/Auth Secret to already be approved as a <b>Wholesale Buyer</b> —
/// documented as returning 403 for any other account type. There is no self-service enablement documented;
/// this must be arranged with Eneba directly before this supplier can fulfill anything for real.
///
/// <b>No idempotency</b>: unlike Bamboo (RequestId)/Visoria (Idempotency-Key)/GlobeTopper (derived
/// order_id, best-effort), Eneba's <c>S_purchaseWholesaleAuctions</c> mutation has no client-reference
/// field anywhere in its documented input shape, and <c>O_orders</c> can only be looked up by Eneba's own
/// <c>orderId</c> — never by any value HAMBOX controls. See <see cref="GetOrderStatusAsync"/>'s remarks
/// for the resulting, unavoidable reconciliation gap.
/// </remarks>
internal sealed class EnebaSupplierProvider(EnebaHttpClient httpClient, IOptions<EnebaProviderOptions> options, ILogger<EnebaSupplierProvider> logger) : ISupplierProvider
{
    public string ProviderType => EnebaProviderConstants.ProviderType;

    /// <summary>Documented per-auction-item bound on <c>S_purchaseWholesaleAuctions</c> (1–2000) — see <see cref="PurchaseAsync"/>'s own range check, which is the authoritative enforcement.</summary>
    public int? MaxQuantityPerPurchase => EnebaProviderConstants.MaxQuantityPerAuctionItem;

    public async Task<SupplierConnectionTestResult> TestConnectionAsync(SupplierProviderContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await httpClient.SearchWholesaleAuctionsAsync(context.SupplierId, context.Credentials, null, null, 1, null, cancellationToken);
            var error = ExtractErrorMessage(response);
            return error is not null
                ? new SupplierConnectionTestResult(false, error)
                : new SupplierConnectionTestResult(true, "Connected — Eneba authenticated and P_wholesaleAuctions responded.");
        }
        catch (Exception ex) when (ex is EnebaApiException or EnebaAmbiguousResponseException)
        {
            logger.LogWarning(ex, "Eneba connection test failed for supplier {SupplierId}.", context.SupplierId);
            return new SupplierConnectionTestResult(false, SafeMessage(ex));
        }
    }

    public async Task<SupplierCredentialValidationResult> ValidateCredentialsAsync(SupplierProviderContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await httpClient.SearchWholesaleAuctionsAsync(context.SupplierId, context.Credentials, null, null, 1, null, cancellationToken);
            var error = ExtractErrorMessage(response);
            return error is not null
                ? new SupplierCredentialValidationResult(false, error)
                : new SupplierCredentialValidationResult(true, null);
        }
        catch (Exception ex) when (ex is EnebaApiException or EnebaAmbiguousResponseException)
        {
            logger.LogWarning(ex, "Eneba credential validation failed for supplier {SupplierId}.", context.SupplierId);
            return new SupplierCredentialValidationResult(false, SafeMessage(ex));
        }
    }

    // Not part of the MVP purchase path — map wholesale auctions manually via Supplier Product Mappings,
    // using SearchCatalogAsync below. Honest stub, matching every other real provider's identical convention.
    public Task<SupplierProductSyncResult> SyncProductsAsync(SupplierProviderContext context, CancellationToken cancellationToken = default) =>
        Task.FromResult(new SupplierProductSyncResult(false, 0, "Eneba catalog sync is not implemented — map products manually via Supplier Product Mappings."));

    /// <summary>
    /// <c>P_wholesaleAuctions</c> is cursor-paginated (<c>first</c>/<c>after</c>), not page-number-based —
    /// unlike Bamboo/Visoria's real <c>PageIndex</c> support or GlobeTopper's "pull everything, page
    /// client-side" (only viable there because its whole catalog is genuinely small — Eneba's is not, and
    /// there is no "everything" endpoint to pull). <see cref="SupplierCatalogQuery.Page"/> &gt; 1 is
    /// resolved by walking the cursor forward one page at a time — <c>Page</c> sequential, provider-side
    /// filtered calls, never a full-catalog pull. Fine for admin's typical shallow search-box paging; not
    /// used anywhere performance-sensitive (<see cref="GetAvailabilityAsync"/> never calls this).
    /// </summary>
    public async Task<SupplierCatalogSearchResult> SearchCatalogAsync(SupplierCatalogQuery query, SupplierProviderContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var pageSize = query.PageSize is < 1 or > 100 ? 20 : query.PageSize;
            var targetPage = Math.Max(1, query.Page);

            IReadOnlyList<EnebaAuction> auctions = [];
            string? after = null;
            for (var i = 1; i <= targetPage; i++)
            {
                var response = await httpClient.SearchWholesaleAuctionsAsync(
                    context.SupplierId, context.Credentials, query.SearchTerm, null, pageSize, after, cancellationToken);

                var error = ExtractErrorMessage(response);
                if (error is not null)
                {
                    return new SupplierCatalogSearchResult(false, [], error);
                }

                var connection = response.Data?.Auctions;
                auctions = connection?.Edges?.Where(e => e.Node is not null).Select(e => e.Node!).ToArray() ?? [];

                if (i < targetPage)
                {
                    if (connection?.PageInfo?.HasNextPage != true)
                    {
                        // Requested page is past the end of the result set.
                        return new SupplierCatalogSearchResult(true, [], null);
                    }

                    after = connection.PageInfo!.EndCursor;
                }
            }

            return new SupplierCatalogSearchResult(true, auctions.Select(ToCatalogItem).ToArray(), null);
        }
        catch (Exception ex) when (ex is EnebaApiException or EnebaAmbiguousResponseException)
        {
            logger.LogWarning(ex, "Eneba catalog search failed for supplier {SupplierId}.", context.SupplierId);
            return new SupplierCatalogSearchResult(false, [], SafeMessage(ex));
        }
    }

    /// <summary>
    /// <see cref="SupplierCatalogItem.MinFaceValue"/>/<see cref="SupplierCatalogItem.MaxFaceValue"/> are
    /// both set to the same single <see cref="EnebaAuction.WholesalePrice"/> — a wholesale auction is one
    /// specific listing at one specific price, not a denomination range like a Bamboo/GlobeTopper gift
    /// card — there is no "range" concept to report here, so min/max collapsing to the same value is
    /// deliberate, not a shortcut. <see cref="EnebaMoney.Amount"/> is documented as the smallest currency
    /// unit (cents), hence the /100 division.
    /// </summary>
    private static SupplierCatalogItem ToCatalogItem(EnebaAuction auction)
    {
        var price = auction.WholesalePrice is { } m ? m.Amount / 100m : (decimal?)null;
        var available = auction.WholesaleStock is null || auction.WholesaleStock > 0;

        return new SupplierCatalogItem(
            auction.Id,
            auction.Product?.Name ?? "Unknown product",
            auction.Merchant?.DisplayName,
            auction.WholesalePrice?.Currency ?? "EUR",
            price,
            price,
            available);
    }

    // No documented bulk inventory/price endpoint distinct from the catalog search above.
    public Task<SupplierInventorySyncResult> SyncInventoryAsync(SupplierProviderContext context, CancellationToken cancellationToken = default) =>
        Task.FromResult(new SupplierInventorySyncResult(false, 0, "Eneba inventory sync is not implemented — no bulk endpoint is documented beyond catalog search."));

    public Task<SupplierPriceSyncResult> SyncPricesAsync(SupplierProviderContext context, CancellationToken cancellationToken = default) =>
        Task.FromResult(new SupplierPriceSyncResult(false, 0, "Eneba price sync is not implemented — no bulk endpoint is documented beyond catalog search."));

    /// <summary>
    /// Genuine, confirmed capability gap: neither <c>P_wholesaleAuctions</c> nor
    /// <c>P_wholesaleAuctionProducts</c> supports filtering by a specific list of auction ids — both only
    /// filter by product attributes/search terms. Pulling Eneba's entire wholesale marketplace to check
    /// presence of a handful of mapped ids (the way <c>GlobeTopperSupplierProvider</c> safely does for its
    /// own, genuinely small, "returns everything" catalog) is not reasonable here — Eneba's marketplace has
    /// no such bound. Reported honestly as <c>IsSuccess: false</c> rather than invented.
    /// </summary>
    public Task<SupplierAvailabilityResult> GetAvailabilityAsync(SupplierAvailabilityQuery query, SupplierProviderContext context, CancellationToken cancellationToken = default) =>
        Task.FromResult(new SupplierAvailabilityResult(false, [],
            "Eneba has no documented way to look up specific wholesale auctions by id in bulk — availability cannot be refreshed per mapping. Re-run the catalog search (Map Products) to confirm an auction is still live."));

    // Eneba's documented purchase flow buys directly — no reservation step is documented anywhere.
    public Task<SupplierReservationResult> ReserveAsync(SupplierReservationRequest request, SupplierProviderContext context, CancellationToken cancellationToken = default) =>
        Task.FromResult(new SupplierReservationResult(false, null, "Eneba does not document a reservation step — purchase directly."));

    // No cancellation/refund mutation is documented anywhere for wholesale purchases.
    public Task<SupplierCancellationResult> CancelAsync(SupplierCancellationRequest request, SupplierProviderContext context, CancellationToken cancellationToken = default) =>
        Task.FromResult(new SupplierCancellationResult(false, "Eneba does not document a cancellation/refund API for wholesale purchases."));

    public async Task<SupplierPurchaseResult> PurchaseAsync(SupplierPurchaseRequest request, SupplierProviderContext context, CancellationToken cancellationToken = default)
    {
        if (request.Quantity is < EnebaProviderConstants.MinQuantityPerAuctionItem or > EnebaProviderConstants.MaxQuantityPerAuctionItem)
        {
            return new SupplierPurchaseResult(false, null, null, SupplierFulfillmentFailureCategory.InvalidConfiguration,
                $"Eneba wholesale purchases must be between {EnebaProviderConstants.MinQuantityPerAuctionItem} and {EnebaProviderConstants.MaxQuantityPerAuctionItem} units per auction — requested quantity {request.Quantity} is out of range.");
        }

        if (!Guid.TryParse(request.ExternalProductId, out _))
        {
            return new SupplierPurchaseResult(false, null, null, SupplierFulfillmentFailureCategory.InvalidConfiguration,
                "The supplier product mapping's external product id is not a valid Eneba auction id (UUID).");
        }

        // No try/catch around EnebaAmbiguousResponseException here — it (and any other unexpected
        // exception) propagates to the caller by design, per ISupplierProvider.PurchaseAsync's documented
        // ambiguity contract. Unlike Bamboo/Visoria/GlobeTopper, an ambiguous outcome here that occurs
        // BEFORE an orderId is captured is permanently unreconcilable for Eneba — see GetOrderStatusAsync's remarks.
        EnebaGraphQlResponse<EnebaPurchaseWholesaleAuctionsData> response;
        try
        {
            response = await httpClient.PurchaseWholesaleAuctionsAsync(context.SupplierId, context.Credentials, request.ExternalProductId, request.Quantity, cancellationToken);
        }
        catch (EnebaApiException ex)
        {
            logger.LogWarning("Eneba purchase definitively rejected for HamboxReferenceId {HamboxReferenceId}: HTTP {StatusCode}.", request.ReferenceId, ex.HttpStatusCode);
            return new SupplierPurchaseResult(false, null, null, MapHttpFailureCategory(ex), SafeMessage(ex));
        }

        var result = response.Data?.Result;

        // A captured orderId is trusted and returned regardless of whether GraphQL `errors` also came
        // back alongside it — never discard a real acceptance because of an unrelated partial error.
        if (!string.IsNullOrWhiteSpace(result?.OrderId))
        {
            logger.LogInformation("Eneba purchase accepted for HamboxReferenceId {HamboxReferenceId}: orderId {OrderId}.", request.ReferenceId, result.OrderId);
            // success:true only confirms the checkout was queued, never delivery (documented verbatim) —
            // DeliveredCodes stays null, driving SupplierFulfillment to Submitted, resolved later via
            // GetOrderStatusAsync, exactly like Bamboo's Place Order.
            return new SupplierPurchaseResult(true, result.OrderId, null, null, null);
        }

        var errorMessage = ExtractErrorMessage(response);
        if (errorMessage is not null)
        {
            logger.LogWarning("Eneba purchase rejected via GraphQL errors for HamboxReferenceId {HamboxReferenceId}: {Message}", request.ReferenceId, errorMessage);
            return new SupplierPurchaseResult(false, null, null, SupplierFulfillmentFailureCategory.UnknownProviderState, errorMessage);
        }

        if (result is { Success: false })
        {
            return new SupplierPurchaseResult(false, null, null, SupplierFulfillmentFailureCategory.UnknownProviderState,
                "Eneba reported success: false with no further detail and no orderId.");
        }

        // Nothing usable at all — no orderId, no GraphQL errors, no explicit success:false. Malformed;
        // never trust it — resolve via reconciliation like any other ambiguous outcome, since there is
        // nothing to distinguish this from a partially-received response.
        throw new EnebaAmbiguousResponseException("Eneba purchase response had no orderId and no GraphQL errors — cannot confirm the outcome.");
    }

    public async Task<SupplierOrderStatusResult> GetOrderStatusAsync(SupplierOrderStatusQuery query, SupplierProviderContext context, CancellationToken cancellationToken = default)
    {
        // Genuine, documented limitation: S_purchaseWholesaleAuctions has no client-reference/idempotency
        // field anywhere in its input, and O_orders can only be filtered by Eneba's own orderId — there is
        // no lookup by any client-supplied reference (unlike Bamboo's RequestId or Visoria's
        // Idempotency-Key, both of which ARE HamboxReferenceId). A fulfillment whose purchase call was
        // ambiguous BEFORE an orderId was ever captured cannot be reconciled by this provider at all —
        // intentionally NOT worked around (nothing documented to work around it with, and guessing via
        // "list recent orders and correlate by time/amount" was explicitly ruled out as unsafe). It
        // surfaces as a permanently ambiguous (SupplierFulfillmentStatus.Unknown) attempt requiring manual
        // reconciliation via the Eneba merchant dashboard. See docs/integrations/suppliers/README.md §19.
        if (string.IsNullOrWhiteSpace(query.ProviderOrderId))
        {
            throw new EnebaAmbiguousResponseException(
                "Eneba has no order lookup by client reference — without a captured orderId (ProviderOrderId), this fulfillment's outcome cannot be queried. Manual reconciliation via the Eneba merchant dashboard is required.");
        }

        var ordersResponse = await httpClient.GetOrdersAsync(context.SupplierId, context.Credentials, [query.ProviderOrderId], cancellationToken);
        var ordersError = ExtractErrorMessage(ordersResponse);
        if (ordersError is not null)
        {
            throw new EnebaAmbiguousResponseException($"Eneba O_orders returned GraphQL errors: {ordersError}");
        }

        var order = ordersResponse.Data?.Orders?.Edges?.Select(e => e.Node).FirstOrDefault(n => n is not null);
        if (order is null)
        {
            throw new EnebaAmbiguousResponseException($"Eneba returned no order for orderId '{query.ProviderOrderId}'.");
        }

        return order.OrderState switch
        {
            EnebaOrderState.New or EnebaOrderState.Cart =>
                new SupplierOrderStatusResult(SupplierProviderOrderStatus.Processing, order.Id, null, null, null),

            EnebaOrderState.Cancelled =>
                new SupplierOrderStatusResult(SupplierProviderOrderStatus.Failed, order.Id, [], SupplierFulfillmentFailureCategory.UnknownProviderState, "Eneba order was cancelled."),

            // Documented cause for at least one case: "If the auction does not have enough stock when
            // checkout is processed, the action will end in FAILED state" — but O_orders exposes no
            // reason field, so a more specific category than UnknownProviderState would be guessed, not documented.
            EnebaOrderState.Failed =>
                new SupplierOrderStatusResult(SupplierProviderOrderStatus.Failed, order.Id, [], SupplierFulfillmentFailureCategory.UnknownProviderState,
                    "Eneba order failed (e.g. insufficient auction stock at checkout time — Eneba exposes no more specific reason)."),

            EnebaOrderState.Fulfilled => await ResolveFulfilledOrderAsync(context, order, cancellationToken),

            _ => new SupplierOrderStatusResult(SupplierProviderOrderStatus.Unknown, order.Id, null, null, $"Unrecognized Eneba orderState: '{order.OrderState}'."),
        };
    }

    /// <summary>
    /// FULFILLED only means Eneba considers the order complete — the delivered keys still have to be
    /// pulled through the separate, genuinely asynchronous export flow (<c>O_exportOrderKeys</c> →
    /// poll <c>O_orderExport</c> → download → decrypt). Bounded to <see cref="EnebaProviderOptions.ExportPollAttempts"/>
    /// short polls so one reconciliation call never blocks indefinitely — if the export isn't ready within
    /// that budget, this returns <see cref="SupplierProviderOrderStatus.Processing"/> (not a failure) and
    /// the next sweep tick simply calls this again (re-triggering the export, which the documentation
    /// describes as safe: "call again to produce a fresh export").
    /// </summary>
    private async Task<SupplierOrderStatusResult> ResolveFulfilledOrderAsync(SupplierProviderContext context, EnebaOrder order, CancellationToken cancellationToken)
    {
        var item = order.Items?.FirstOrDefault();
        if (item is null)
        {
            throw new EnebaAmbiguousResponseException($"Eneba order '{order.Id}' is FULFILLED but returned no items.");
        }

        var settings = EnebaHttpClient.ParseSettings(context.Credentials);
        if (string.IsNullOrWhiteSpace(settings.AccountEmail))
        {
            throw new EnebaAmbiguousResponseException(
                $"Eneba order '{order.Id}' is FULFILLED but this supplier has no accountEmail configured in OAuth settings — required to decrypt the key-export archive. Configure it and retry.");
        }

        var exportResponse = await httpClient.ExportOrderKeysAsync(context.SupplierId, context.Credentials, order.EntryToken, cancellationToken);
        var exportError = ExtractErrorMessage(exportResponse);
        if (exportError is not null || exportResponse.Data?.Result?.Success != true)
        {
            throw new EnebaAmbiguousResponseException($"Eneba O_exportOrderKeys did not report success for order '{order.Id}': {exportError ?? "success=false"}.");
        }

        string? downloadUrl = null;
        for (var attempt = 0; attempt < options.Value.ExportPollAttempts && downloadUrl is null; attempt++)
        {
            if (attempt > 0)
            {
                await Task.Delay(TimeSpan.FromSeconds(options.Value.ExportPollDelaySeconds), cancellationToken);
            }

            var pollResponse = await httpClient.GetOrderExportAsync(context.SupplierId, context.Credentials, order.EntryToken, cancellationToken);
            var pollError = ExtractErrorMessage(pollResponse);
            if (pollError is not null)
            {
                throw new EnebaAmbiguousResponseException($"Eneba O_orderExport returned GraphQL errors: {pollError}");
            }

            if (string.Equals(pollResponse.Data?.Export?.Status, EnebaOrderExportStatus.Completed, StringComparison.OrdinalIgnoreCase))
            {
                downloadUrl = pollResponse.Data?.Export?.DownloadUrl;
            }
        }

        if (string.IsNullOrWhiteSpace(downloadUrl))
        {
            logger.LogInformation(
                "Eneba key export for order {OrderId} not ready after {Attempts} polls — will retry on the next reconciliation sweep.",
                order.Id, options.Value.ExportPollAttempts);
            return new SupplierOrderStatusResult(SupplierProviderOrderStatus.Processing, order.Id, null, null, "Eneba key export still processing.");
        }

        var archiveBytes = await httpClient.DownloadArchiveAsync(downloadUrl, cancellationToken);

        EnebaKeyExtractionResult extraction;
        try
        {
            extraction = EnebaArchiveReader.ExtractKeys(archiveBytes, settings.AccountEmail!, order.OrderNumber, item.SellableSlug, item.ShortId);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Wrong password, corrupt/unexpected archive structure — never guessed; resolved by retrying
            // reconciliation, or ultimately by manual intervention.
            throw new EnebaAmbiguousResponseException($"Eneba key-export archive for order '{order.Id}' could not be read/decrypted.", ex);
        }

        switch (extraction.Outcome)
        {
            case EnebaKeyExtractionOutcome.Extracted:
                return new SupplierOrderStatusResult(SupplierProviderOrderStatus.Succeeded, order.Id, extraction.Keys, null, null);

            case EnebaKeyExtractionOutcome.DirectoryNotFound:
                throw new EnebaAmbiguousResponseException(
                    $"Eneba key-export archive for order '{order.Id}' did not contain the expected item directory ({order.OrderNumber}/{item.SellableSlug}/{item.ShortId}/) — cannot confirm delivery yet.");

            case EnebaKeyExtractionOutcome.ImageKeysOnly:
            default:
                logger.LogError(
                    "Eneba order {OrderId} delivered image-format keys, which this integration cannot extract as text — manual retrieval required.", order.Id);
                return new SupplierOrderStatusResult(SupplierProviderOrderStatus.Failed, order.Id, [], SupplierFulfillmentFailureCategory.UnknownProviderState,
                    $"Eneba delivered image-format key(s) for order {order.OrderNumber} — this integration only extracts text keys (keys.txt). Retrieve manually from the Eneba merchant dashboard.");
        }
    }

    private static string? ExtractErrorMessage<TData>(EnebaGraphQlResponse<TData> response) =>
        response.Errors is { Count: > 0 } errors
            ? string.Join("; ", errors.Select(e => e.Message).Where(m => !string.IsNullOrWhiteSpace(m)))
            : null;

    private static SupplierFulfillmentFailureCategory MapHttpFailureCategory(EnebaApiException ex) => ex.HttpStatusCode switch
    {
        0 => SupplierFulfillmentFailureCategory.InvalidConfiguration,
        401 or 403 => SupplierFulfillmentFailureCategory.AuthenticationFailed,
        429 => SupplierFulfillmentFailureCategory.ProviderUnavailable,
        _ => SupplierFulfillmentFailureCategory.UnknownProviderState,
    };

    /// <summary>Never the raw exception/response — just the documented-safe message; no Authorization header, access token, Auth Secret, or account email ever reaches an exception message in this provider.</summary>
    private static string SafeMessage(Exception ex) => ex.Message;
}
