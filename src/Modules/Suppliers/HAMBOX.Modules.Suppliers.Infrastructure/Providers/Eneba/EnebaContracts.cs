using System.Text.Json.Serialization;

namespace HAMBOX.Modules.Suppliers.Infrastructure.Providers.Eneba;

/// <summary>
/// Every Eneba-specific wire type lives in this one file, isolated inside the provider boundary —
/// nothing outside <c>Providers/Eneba/</c> ever references these. Field shapes are transcribed from the
/// documentation prose at https://api.eneba.com/documentation/ (no GraphQL introspection or Postman
/// collection was available in this environment, and no real Eneba API credentials exist yet to verify
/// against) — see docs/integrations/suppliers/README.md for exactly which shapes are documented vs
/// inferred (GraphQL argument names in particular — the docs describe input/output fields but rarely the
/// literal argument keyword), and re-verify with real sandbox access before trusting this in production.
/// </summary>
internal static class EnebaContracts
{
    // GraphQL mutation/query documents. Kept centrally so EnebaHttpClient stays about transport, not
    // query text. Argument names follow standard GraphQL convention (a single "input" object for
    // mutations with a dedicated input type, top-level scalar arguments for queries) since the
    // documentation prose never shows a full literal request — unverified, see file remarks above.

    public const string WholesaleAuctionsQuery = """
        query WholesaleAuctions($search: String, $productIds: [P_Uuid!], $first: Int, $after: String) {
          P_wholesaleAuctions(search: $search, productIds: $productIds, first: $first, after: $after) {
            totalCount
            pageInfo { hasNextPage hasPreviousPage startCursor endCursor }
            edges {
              cursor
              node {
                id
                wholesalePrice { amount currency }
                wholesaleStock
                merchant { displayName slug }
                product { id name slug productType }
              }
            }
          }
        }
        """;

    public const string PurchaseWholesaleAuctionsMutation = """
        mutation PurchaseWholesaleAuctions($input: S_API_PurchaseWholesaleAuctionsInput!) {
          S_purchaseWholesaleAuctions(input: $input) {
            success
            orderId
            actionId
          }
        }
        """;

    public const string ActionQuery = """
        query Action($actionId: A_Uuid!) {
          A_action(actionId: $actionId) {
            id
            state
          }
        }
        """;

    public const string OrdersQuery = """
        query Orders($orderIds: [O_Uuid!]) {
          O_orders(orderIds: $orderIds) {
            edges {
              node {
                id
                orderNumber
                entryToken
                orderState
                paymentState
                createdAt
                items { shortId sellableName sellableSlug quantity }
              }
            }
          }
        }
        """;

    public const string ExportOrderKeysMutation = """
        mutation ExportOrderKeys($input: O_API_ExportOrderKeysInput!) {
          O_exportOrderKeys(input: $input) {
            success
          }
        }
        """;

    public const string OrderExportQuery = """
        query OrderExport($entryToken: String!) {
          O_orderExport(entryToken: $entryToken) {
            status
            downloadUrl
          }
        }
        """;
}

// ─── OAuth ──────────────────────────────────────────────────────────────────

/// <summary>
/// The only credential shape this provider stores in <c>Supplier.OAuthSettingsJson</c> (encrypted at
/// rest via the existing mechanism — see <c>SuppliersDbContext.ApplyCredentialEncryption</c>).
/// <see cref="AccountEmail"/> is NOT an Eneba API credential — it is the login email of the Eneba account
/// that will place orders, required only because the key-export archive is encrypted with it (see
/// <see cref="EnebaArchiveReader"/>). It is stored alongside the real secrets (not in the non-secret
/// <c>Supplier.SettingsJson</c>) because it functions as a decryption password for delivered license
/// keys — treated as sensitive even though it is technically just an email address.
/// </summary>
internal sealed record EnebaOAuthSettings(
    [property: JsonPropertyName("authId")] string? AuthId,
    [property: JsonPropertyName("authSecret")] string? AuthSecret,
    [property: JsonPropertyName("accountEmail")] string? AccountEmail);

internal sealed record EnebaOAuthTokenResponse(
    [property: JsonPropertyName("access_token")] string? AccessToken,
    [property: JsonPropertyName("expires_in")] int? ExpiresIn,
    [property: JsonPropertyName("refresh_token")] string? RefreshToken,
    [property: JsonPropertyName("token_type")] string? TokenType);

// ─── GraphQL envelope ───────────────────────────────────────────────────────

internal sealed record EnebaGraphQlRequest(
    [property: JsonPropertyName("query")] string Query,
    [property: JsonPropertyName("variables")] object? Variables);

/// <summary>
/// Standard GraphQL response shape — <see cref="Errors"/> non-empty is a definite, in-band business/
/// validation failure (never treated as ambiguous, since it arrives as a complete, parsed response), with
/// no documented machine-readable error-code taxonomy (no <c>extensions.code</c> shape was found anywhere
/// in the documentation) — see <c>EnebaSupplierProvider</c>'s error mapping remarks for how this is
/// handled without guessing a category that isn't actually documented.
/// </summary>
internal sealed record EnebaGraphQlResponse<TData>(
    [property: JsonPropertyName("data")] TData? Data,
    [property: JsonPropertyName("errors")] IReadOnlyList<EnebaGraphQlError>? Errors);

internal sealed record EnebaGraphQlError(
    [property: JsonPropertyName("message")] string? Message,
    [property: JsonPropertyName("path")] IReadOnlyList<string>? Path);

// ─── P_wholesaleAuctions (catalog) ──────────────────────────────────────────

internal sealed record EnebaWholesaleAuctionsData(
    [property: JsonPropertyName("P_wholesaleAuctions")] EnebaAuctionConnection? Auctions);

internal sealed record EnebaAuctionConnection(
    [property: JsonPropertyName("totalCount")] int TotalCount,
    [property: JsonPropertyName("pageInfo")] EnebaPageInfo? PageInfo,
    [property: JsonPropertyName("edges")] IReadOnlyList<EnebaAuctionEdge>? Edges);

internal sealed record EnebaAuctionEdge(
    [property: JsonPropertyName("cursor")] string? Cursor,
    [property: JsonPropertyName("node")] EnebaAuction? Node);

internal sealed record EnebaAuction(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("wholesalePrice")] EnebaMoney? WholesalePrice,
    [property: JsonPropertyName("wholesaleStock")] int? WholesaleStock,
    [property: JsonPropertyName("merchant")] EnebaAuctionMerchant? Merchant,
    [property: JsonPropertyName("product")] EnebaProduct? Product);

/// <summary><see cref="Amount"/> is documented as "Amount in the smallest currency unit" (i.e. cents) — divided by 100 wherever this is surfaced as a decimal face value; never treated as a whole-currency-unit amount.</summary>
internal sealed record EnebaMoney(
    [property: JsonPropertyName("amount")] int Amount,
    [property: JsonPropertyName("currency")] string Currency);

internal sealed record EnebaAuctionMerchant(
    [property: JsonPropertyName("displayName")] string? DisplayName,
    [property: JsonPropertyName("slug")] string? Slug);

internal sealed record EnebaProduct(
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("slug")] string? Slug,
    [property: JsonPropertyName("productType")] string? ProductType);

internal sealed record EnebaPageInfo(
    [property: JsonPropertyName("hasNextPage")] bool HasNextPage,
    [property: JsonPropertyName("hasPreviousPage")] bool HasPreviousPage,
    [property: JsonPropertyName("startCursor")] string? StartCursor,
    [property: JsonPropertyName("endCursor")] string? EndCursor);

// ─── S_purchaseWholesaleAuctions (purchase) ─────────────────────────────────

internal sealed record EnebaPurchaseWholesaleAuctionsData(
    [property: JsonPropertyName("S_purchaseWholesaleAuctions")] EnebaPurchaseResponse? Result);

internal sealed record EnebaPurchaseInput(
    [property: JsonPropertyName("items")] IReadOnlyList<EnebaPurchaseItem> Items);

internal sealed record EnebaPurchaseItem(
    [property: JsonPropertyName("auctionId")] string AuctionId,
    [property: JsonPropertyName("quantity")] int Quantity);

/// <summary>
/// <see cref="Success"/> true only confirms the checkout was queued — never delivery (documented
/// verbatim: "A success: true response only confirms the checkout was queued — it does not mean the
/// purchase has completed"). <see cref="Success"/> false with no <see cref="EnebaGraphQlResponse{T}.Errors"/>
/// is not documented anywhere as a real possibility — treated defensively the same as a GraphQL error
/// (definite rejection) rather than assumed to never happen.
/// </summary>
internal sealed record EnebaPurchaseResponse(
    [property: JsonPropertyName("success")] bool Success,
    [property: JsonPropertyName("orderId")] string? OrderId,
    [property: JsonPropertyName("actionId")] string? ActionId);

// ─── A_action (checkout progression) ────────────────────────────────────────

internal sealed record EnebaActionData(
    [property: JsonPropertyName("A_action")] EnebaAction? Action);

internal sealed record EnebaAction(
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("state")] string? State);

internal static class EnebaActionState
{
    public const string New = "NEW";
    public const string Completed = "COMPLETED";
    public const string Failed = "FAILED";
}

// ─── O_orders (order status / reconciliation) ───────────────────────────────

internal sealed record EnebaOrdersData(
    [property: JsonPropertyName("O_orders")] EnebaOrderConnection? Orders);

internal sealed record EnebaOrderConnection(
    [property: JsonPropertyName("edges")] IReadOnlyList<EnebaOrderEdge>? Edges);

internal sealed record EnebaOrderEdge(
    [property: JsonPropertyName("node")] EnebaOrder? Node);

internal sealed record EnebaOrder(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("orderNumber")] string OrderNumber,
    [property: JsonPropertyName("entryToken")] string EntryToken,
    [property: JsonPropertyName("orderState")] string OrderState,
    [property: JsonPropertyName("paymentState")] string? PaymentState,
    [property: JsonPropertyName("createdAt")] DateTimeOffset? CreatedAt,
    [property: JsonPropertyName("items")] IReadOnlyList<EnebaOrderItem>? Items);

/// <summary><see cref="Quantity"/> is the units purchased for this line — this integration only ever purchases one auction per <c>SupplierPurchaseRequest</c>, so exactly one item is expected per order.</summary>
internal sealed record EnebaOrderItem(
    [property: JsonPropertyName("shortId")] string ShortId,
    [property: JsonPropertyName("sellableName")] string? SellableName,
    [property: JsonPropertyName("sellableSlug")] string SellableSlug,
    [property: JsonPropertyName("quantity")] int Quantity);

/// <summary>Documented enum values (<c>O_OrderState</c>) — any other value maps to <c>Unknown</c>, never guessed. See <c>EnebaSupplierProvider.MapOrderState</c>.</summary>
internal static class EnebaOrderState
{
    public const string Cart = "CART";
    public const string New = "NEW";
    public const string Cancelled = "CANCELLED";
    public const string Fulfilled = "FULFILLED";
    public const string Failed = "FAILED";
}

// ─── Key export/download ────────────────────────────────────────────────────

internal sealed record EnebaExportOrderKeysData(
    [property: JsonPropertyName("O_exportOrderKeys")] EnebaExportOrderKeysResponse? Result);

internal sealed record EnebaExportOrderKeysInput(
    [property: JsonPropertyName("entryToken")] string EntryToken);

internal sealed record EnebaExportOrderKeysResponse(
    [property: JsonPropertyName("success")] bool Success);

internal sealed record EnebaOrderExportData(
    [property: JsonPropertyName("O_orderExport")] EnebaOrderExport? Export);

internal sealed record EnebaOrderExport(
    [property: JsonPropertyName("status")] string? Status,
    [property: JsonPropertyName("downloadUrl")] string? DownloadUrl);

internal static class EnebaOrderExportStatus
{
    public const string Completed = "COMPLETED";
}
