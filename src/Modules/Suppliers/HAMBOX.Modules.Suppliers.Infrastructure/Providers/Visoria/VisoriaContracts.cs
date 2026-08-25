using System.Text.Json.Serialization;

namespace HAMBOX.Modules.Suppliers.Infrastructure.Providers.Visoria;

/// <summary>
/// Every Visoria-specific wire type lives in this one file, isolated inside the provider boundary —
/// nothing outside <c>Providers/Visoria/</c> ever references these. Shapes are transcribed directly from
/// the supplied OpenAPI spec (docs/integrations/suppliers/api-2.json), not guessed.
/// </summary>
internal sealed record VisoriaCreateOrderRequestBody(
    [property: JsonPropertyName("items")] IReadOnlyList<VisoriaOrderLineItem> Items,
    [property: JsonPropertyName("currency_code")] string CurrencyCode);

internal sealed record VisoriaOrderLineItem(
    [property: JsonPropertyName("product_id")] string ProductId,
    [property: JsonPropertyName("quantity")] int Quantity,
    [property: JsonPropertyName("face_value")] decimal FaceValue);

internal sealed record VisoriaOrder(
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("number")] string? Number,
    [property: JsonPropertyName("status")] string? Status,
    [property: JsonPropertyName("items")] IReadOnlyList<VisoriaOrderItem>? Items);

internal sealed record VisoriaOrderItem(
    [property: JsonPropertyName("product_id")] string? ProductId,
    [property: JsonPropertyName("quantity")] int Quantity,
    [property: JsonPropertyName("keys")] IReadOnlyList<VisoriaOrderKey>? Keys);

/// <summary>
/// <see cref="Key"/>/<see cref="Pin"/> are the redemption secret — treated exactly like a password
/// everywhere this type is touched: never logged, encrypted immediately once handed to Commerce, never
/// exposed outside this provider boundary except as the already-generic
/// <c>SupplierPurchaseResult.DeliveredCodes</c>/<c>SupplierOrderStatusResult.DeliveredCodes</c> string
/// collection. Both are only actually populated when the API key carries the <c>keys:read</c> scope —
/// otherwise Visoria still reports <see cref="Status"/> but omits the values, which this provider treats
/// as "not deliverable" (see <c>VisoriaSupplierProvider.ExtractDeliveredCodes</c>), never as a delivered code.
/// </summary>
internal sealed record VisoriaOrderKey(
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("status")] string? Status,
    [property: JsonPropertyName("key")] string? Key,
    [property: JsonPropertyName("pin")] string? Pin);

/// <summary>Documented order-level status strings.</summary>
internal static class VisoriaOrderStatus
{
    public const string Progressing = "PROGRESSING";
    public const string Completed = "COMPLETED";
    public const string Cancelled = "CANCELLED";
}

/// <summary>Only "DELIVERED" counts as a genuinely retrievable code — "RESERVED"/"REFUNDED" never carry a usable value.</summary>
internal static class VisoriaKeyStatus
{
    public const string Delivered = "DELIVERED";
}

internal sealed record VisoriaProduct(
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("categories")] IReadOnlyList<VisoriaCategory>? Categories,
    [property: JsonPropertyName("market_price")] decimal MarketPrice,
    [property: JsonPropertyName("currency_code")] string? CurrencyCode,
    [property: JsonPropertyName("denomination")] VisoriaDenomination? Denomination,
    [property: JsonPropertyName("orderable")] bool Orderable,
    [property: JsonPropertyName("stock")] int Stock,
    [property: JsonPropertyName("stock_unlimited")] bool StockUnlimited,
    [property: JsonPropertyName("fulfillment_type")] string? FulfillmentType);

internal sealed record VisoriaCategory(
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("name")] string? Name);

internal sealed record VisoriaDenomination(
    [property: JsonPropertyName("type")] string? Type,
    [property: JsonPropertyName("min")] decimal Min,
    [property: JsonPropertyName("max")] decimal Max);

/// <summary>"OPEN" products need a real chosen amount in <c>face_value</c>; every other type (including RECHARGE) requires exactly 1 — per the documentation, never guessed.</summary>
internal static class VisoriaDenominationType
{
    public const string Open = "OPEN";
}

/// <summary>
/// RECHARGE products require per-unit <c>recharge_data</c> (a customer game/phone account identifier)
/// on each line item — a concept HAMBOX's generic <c>SupplierPurchaseRequest</c> has no field for and
/// does not currently collect anywhere in the automated-supplier checkout path. Not supported by this
/// integration; see <c>VisoriaSupplierProvider.PurchaseAsync</c>'s explicit fail-closed check.
/// </summary>
internal static class VisoriaFulfillmentType
{
    public const string Pin = "PIN";
    public const string Recharge = "RECHARGE";
}

internal sealed record VisoriaProductListResponse(
    [property: JsonPropertyName("data")] IReadOnlyList<VisoriaProduct>? Data);

internal sealed record VisoriaBalance(
    [property: JsonPropertyName("currency_code")] string? CurrencyCode,
    [property: JsonPropertyName("livemode")] bool Livemode);

/// <summary>
/// The exact set of error codes is not exhaustively documented (only representative examples:
/// AUTHx01/02, REQUESTx01-04, VALIDATORx01, DBx01, ERRORx01) — <see cref="Code"/> is kept for logging
/// only, never switched on for failure-category mapping beyond the documented examples (see
/// <c>VisoriaSupplierProvider.MapFailureCategory</c>, which maps by HTTP status instead).
/// </summary>
internal sealed record VisoriaErrorBody(
    [property: JsonPropertyName("code")] string? Code,
    [property: JsonPropertyName("message")] string? Message);
