using System.Text.Json.Serialization;

namespace HAMBOX.Modules.Suppliers.Infrastructure.Providers.CodesWholesale;

/// <summary>
/// Standard RFC 6749 client-credentials token response. The PHP SDK never parses this itself — it hands
/// token handling to a generic Guzzle OAuth2 middleware — so this shape is the standard OAuth2 response,
/// not a captured real example; see <c>CodesWholesaleProviderConstants</c>'s remarks.
/// </summary>
internal sealed class CodesWholesaleTokenResponse
{
    [JsonPropertyName("access_token")]
    public string? AccessToken { get; set; }

    [JsonPropertyName("expires_in")]
    public int? ExpiresIn { get; set; }

    [JsonPropertyName("token_type")]
    public string? TokenType { get; set; }
}

/// <summary>Standard RFC 6749 error response, returned by the token endpoint on invalid credentials.</summary>
internal sealed class CodesWholesaleOAuthErrorResponse
{
    [JsonPropertyName("error")]
    public string? Error { get; set; }

    [JsonPropertyName("error_description")]
    public string? ErrorDescription { get; set; }
}

/// <summary>
/// Business-API error envelope (confirmed field names: <c>Resource/Error.php</c>) — returned by every
/// <c>/v2/...</c> endpoint on a 4xx. <see cref="Code"/> is CodesWholesale's numeric business error code
/// (e.g. 10002 = insufficient balance, 20001 = product not found — confirmed in
/// <c>examples/create-order.php</c>), distinct from the HTTP status.
/// </summary>
internal sealed class CodesWholesaleErrorResponse
{
    [JsonPropertyName("status")]
    public int? Status { get; set; }

    [JsonPropertyName("code")]
    public int? Code { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("developerMessage")]
    public string? DeveloperMessage { get; set; }

    [JsonPropertyName("moreInfo")]
    public string? MoreInfo { get; set; }
}

internal sealed class CodesWholesaleAccount
{
    [JsonPropertyName("fullName")]
    public string? FullName { get; set; }

    [JsonPropertyName("email")]
    public string? Email { get; set; }

    [JsonPropertyName("currentBalance")]
    public decimal? CurrentBalance { get; set; }

    [JsonPropertyName("currentCredit")]
    public decimal? CurrentCredit { get; set; }

    [JsonPropertyName("totalToUse")]
    public decimal? TotalToUse { get; set; }
}

/// <summary>One quantity-tier price entry (confirmed field names: <c>Resource/Price.php</c>, <c>Product::getDefaultPrice</c>/<c>getLowestPrice</c>). <see cref="From"/>/<see cref="To"/> are the quantity range this <see cref="Value"/> applies to — genuine documented quantity-based pricing, not invented.</summary>
internal sealed class CodesWholesalePrice
{
    [JsonPropertyName("price")]
    public decimal Value { get; set; }

    [JsonPropertyName("priceRangeLabel")]
    public string? RangeLabel { get; set; }

    [JsonPropertyName("from")]
    public decimal? From { get; set; }

    [JsonPropertyName("to")]
    public decimal? To { get; set; }
}

internal sealed class CodesWholesaleImage
{
    [JsonPropertyName("image")]
    public string? Url { get; set; }

    [JsonPropertyName("format")]
    public string? Format { get; set; }
}

/// <summary>Confirmed field names: <c>Resource/Product.php</c>/<c>Resource/FullProduct.php</c> and the Go SDK's identical <c>Product</c> struct tags.</summary>
internal sealed class CodesWholesaleProduct
{
    [JsonPropertyName("productId")]
    public string? ProductId { get; set; }

    [JsonPropertyName("identifier")]
    public string? Identifier { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("platform")]
    public string? Platform { get; set; }

    /// <summary>Live stock count — the only quantity-typed availability signal CodesWholesale documents (confirmed: <c>Product::getStockQuantity</c>).</summary>
    [JsonPropertyName("quantity")]
    public int? Quantity { get; set; }

    [JsonPropertyName("regions")]
    public IReadOnlyList<string>? Regions { get; set; }

    [JsonPropertyName("languages")]
    public IReadOnlyList<string>? Languages { get; set; }

    [JsonPropertyName("prices")]
    public IReadOnlyList<CodesWholesalePrice>? Prices { get; set; }

    [JsonPropertyName("images")]
    public IReadOnlyList<CodesWholesaleImage>? Images { get; set; }
}

/// <summary>Envelope shape confirmed by <c>AbstractCollectionResource</c>'s <c>$collectionField = "items"</c> and the Go SDK's identical <c>Item{Items []Product} json:"items"</c> struct — no server-side pagination fields exist on this response (confirmed: <c>Page</c> is purely a client-side SDK wrapper, never populated from a response field).</summary>
internal sealed class CodesWholesaleProductListResponse
{
    [JsonPropertyName("items")]
    public IReadOnlyList<CodesWholesaleProduct>? Items { get; set; }
}

/// <summary>Request body for <c>POST /v2/orders</c>. Field name confirmed: <c>OrderRequest::CLIENT_ORDER_ID = "orderId"</c> — CodesWholesale's own name for the caller-supplied reference sent on the wire, distinct from the <c>clientOrderId</c> name it's echoed back under in the response (see <see cref="CodesWholesaleOrder"/>).</summary>
internal sealed class CodesWholesaleOrderRequest
{
    [JsonPropertyName("products")]
    public required IReadOnlyList<CodesWholesaleOrderProductEntry> Products { get; init; }

    /// <summary>Sent as <see cref="Application.Abstractions.SupplierPurchaseRequest.ReferenceId"/> (HAMBOX's <c>SupplierFulfillment.HamboxReferenceId</c>) — see <c>ISupplierProvider.PurchaseAsync</c>'s idempotency contract.</summary>
    [JsonPropertyName("orderId")]
    public string? OrderId { get; init; }

    /// <summary>Defaults false in this integration (see <see cref="CodesWholesaleProviderOptions"/>'s remarks) — CodesWholesale's own SDK default is <see langword="true"/>, but leaving pre-order enabled would let an automated HAMBOX purchase sit unresolved for up to 14 days (CodesWholesale's documented pre-order assignment window) with no codes delivered.</summary>
    [JsonPropertyName("allowPreOrder")]
    public bool AllowPreOrder { get; init; }
}

internal sealed class CodesWholesaleOrderProductEntry
{
    [JsonPropertyName("productId")]
    public required string ProductId { get; init; }

    [JsonPropertyName("quantity")]
    public required int Quantity { get; init; }
}

/// <summary>Confirmed field names: <c>Resource/Order.php</c> constants.</summary>
internal sealed class CodesWholesaleOrder
{
    [JsonPropertyName("orderId")]
    public string? OrderId { get; set; }

    [JsonPropertyName("clientOrderId")]
    public string? ClientOrderId { get; set; }

    [JsonPropertyName("totalPrice")]
    public decimal? TotalPrice { get; set; }

    [JsonPropertyName("products")]
    public IReadOnlyList<CodesWholesaleProductResponse>? Products { get; set; }

    /// <summary>
    /// Raw order-level status string. CodesWholesale's own PHP SDK exposes this as a bare
    /// <c>getStatus()</c> passthrough with no enum/vocabulary anywhere in the SDK — no confirmed example
    /// value was ever observed in any available source. Never pattern-matched by this integration for a
    /// go/no-go decision; outcome is instead derived from each delivered <see cref="CodesWholesaleCode.Status"/>
    /// (a genuinely documented, literal-string vocabulary — see <c>CodesWholesaleProviderConstants.CodeStatusText</c>/
    /// <c>CodeStatusImage</c>/<c>CodeStatusPreOrder</c>). Carried through only as a diagnostic message.
    /// </summary>
    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("createdOn")]
    public string? CreatedOn { get; set; }
}

internal sealed class CodesWholesaleOrderListResponse
{
    [JsonPropertyName("items")]
    public IReadOnlyList<CodesWholesaleOrder>? Items { get; set; }
}

/// <summary>Confirmed field names: <c>Resource/ProductResponse.php</c>.</summary>
internal sealed class CodesWholesaleProductResponse
{
    [JsonPropertyName("productId")]
    public string? ProductId { get; set; }

    [JsonPropertyName("unitPrice")]
    public decimal? UnitPrice { get; set; }

    [JsonPropertyName("codes")]
    public IReadOnlyList<CodesWholesaleCode>? Codes { get; set; }
}

/// <summary>Confirmed field names and the three literal <see cref="Status"/> values: <c>Resource/Code.php</c>.</summary>
internal sealed class CodesWholesaleCode
{
    [JsonPropertyName("codeId")]
    public string? CodeId { get; set; }

    /// <summary>One of <see cref="CodesWholesaleProviderConstants.CodeStatusText"/>/<see cref="CodesWholesaleProviderConstants.CodeStatusImage"/>/<see cref="CodesWholesaleProviderConstants.CodeStatusPreOrder"/> — confirmed literal strings, not an enum on the wire.</summary>
    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("code")]
    public string? Code { get; set; }

    [JsonPropertyName("filename")]
    public string? FileName { get; set; }
}
