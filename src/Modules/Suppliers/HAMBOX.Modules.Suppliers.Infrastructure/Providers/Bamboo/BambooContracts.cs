using System.Text.Json.Serialization;

namespace HAMBOX.Modules.Suppliers.Infrastructure.Providers.Bamboo;

/// <summary>
/// Every Bamboo-specific wire type lives in this one file, isolated inside the provider boundary —
/// nothing outside <c>Providers/Bamboo/</c> ever references these. Field shapes are transcribed from
/// the documentation prose (no official JSON schema or Postman collection was available in this
/// environment) — see docs/integrations/suppliers/README.md for exactly which fields are documented
/// vs assumed, and verify against the real sandbox before trusting this in production.
/// </summary>
internal sealed record BambooCheckoutRequestBody(
    [property: JsonPropertyName("RequestId")] Guid RequestId,
    [property: JsonPropertyName("AccountId")] long AccountId,
    [property: JsonPropertyName("Products")] IReadOnlyList<BambooCheckoutProduct> Products);

internal sealed record BambooCheckoutProduct(
    [property: JsonPropertyName("ProductId")] long ProductId,
    [property: JsonPropertyName("Quantity")] int Quantity,
    [property: JsonPropertyName("Value")] decimal Value);

// A successful Place Order's body is a bare JSON string (the RequestId), not an object — confirmed
// against the real sandbox in Phase 5, contradicting the documentation prose. No dedicated response
// type is needed for it; BambooHttpClient.PlaceOrderAsync deserializes directly as string.

/// <summary>
/// The exact JSON envelope for a 400 was never documented beyond the reason-code strings. Confirmed
/// against the real sandbox (Phase 5): Bamboo's actual duplicate-RequestId body is
/// <c>{"message":"OrderAlreadyExists"}</c> — the reason code travels in <see cref="Message"/>, not a
/// dedicated field. Kept deliberately tolerant (every field nullable, several plausible name variants)
/// since only one shape has actually been observed for real.
/// </summary>
internal sealed record BambooErrorResponseBody(
    [property: JsonPropertyName("reasonCode")] string? ReasonCode,
    [property: JsonPropertyName("code")] string? Code,
    [property: JsonPropertyName("error")] string? Error,
    [property: JsonPropertyName("message")] string? Message);

internal sealed record BambooAccountsResponseBody(
    [property: JsonPropertyName("accounts")] IReadOnlyList<BambooAccount>? Accounts);

internal sealed record BambooAccount(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("currency")] string? Currency,
    [property: JsonPropertyName("balance")] decimal Balance,
    [property: JsonPropertyName("isActive")] bool IsActive,
    [property: JsonPropertyName("sandboxMode")] bool SandboxMode);

internal sealed record BambooOrderResponseBody(
    [property: JsonPropertyName("orderId")] long? OrderId,
    [property: JsonPropertyName("requestId")] string? RequestId,
    [property: JsonPropertyName("items")] IReadOnlyList<BambooOrderItem>? Items,
    [property: JsonPropertyName("status")] string? Status,
    [property: JsonPropertyName("errorMessage")] string? ErrorMessage);

internal sealed record BambooOrderItem(
    [property: JsonPropertyName("cards")] IReadOnlyList<BambooCard>? Cards);

/// <summary>
/// <see cref="CardCode"/>/<see cref="Pin"/> are the redemption secret — treated exactly like a
/// password/license key everywhere this type is touched: never logged, encrypted immediately once
/// handed to Commerce for persistence, never included in any DTO returned outside the provider
/// boundary except as the already-generic <c>SupplierPurchaseResult.DeliveredCodes</c>/<c>SupplierOrderStatusResult.DeliveredCodes</c>
/// string collection.
/// </summary>
internal sealed record BambooCard(
    [property: JsonPropertyName("cardCode")] string? CardCode,
    [property: JsonPropertyName("pin")] string? Pin,
    [property: JsonPropertyName("status")] string? Status);

/// <summary>Only "Sold" counts as genuinely delivered — every other documented card status means not yet issued or failed.</summary>
internal static class BambooCardStatus
{
    public const string Sold = "Sold";
}

/// <summary>Documented order-level status strings (Get Order response, case as shown in the docs).</summary>
internal static class BambooOrderStatus
{
    public const string Created = "Created";
    public const string Processing = "Processing";
    public const string Pending = "Pending";
    public const string Succeeded = "Succeeded";
    public const string Failed = "Failed";
    public const string PartialFailed = "PartialFailed";
}

/// <summary>Documented 400 reason codes (Place Order). Anything not in this list maps to <c>UnknownProviderState</c> — never guessed.</summary>
internal static class BambooReasonCode
{
    public const string WrongAccount = "WrongAccount";
    public const string InsufficientBalance = "InsufficientBalance";
    public const string InvalidProductIds = "InvalidProductIds";
    public const string InvalidProduct = "InvalidProduct";
    public const string InvalidDenomination = "InvalidDenomination";
    public const string NoProducts = "NoProducts";
    public const string OrderAlreadyExists = "OrderAlreadyExists";
    public const string ClientCatalogNotExist = "ClientCatalogNotExist";
    public const string OrderNotFound = "OrderNotFound";
    public const string CardsLimitExceeded = "CardsLimitExceeded";
    public const string ProductIsOutOfStock = "ProductIsOutOfStock";
    public const string ClientPriceInvalid = "ClientPriceInvalid";
    public const string UserNotEnabled = "UserNotEnabled";
}

/// <summary>
/// Get Catalog response (docs.bamboocardportal.com/docs/get-catalog) — a page of brand-level entries,
/// each carrying its own nested denominations under <see cref="Products"/>. Every field here is
/// display-only catalog data; nothing in this shape is a credential or a card secret.
/// </summary>
internal sealed record BambooCatalogResponseBody(
    [property: JsonPropertyName("pageIndex")] int PageIndex,
    [property: JsonPropertyName("pageSize")] int PageSize,
    [property: JsonPropertyName("count")] int Count,
    [property: JsonPropertyName("items")] IReadOnlyList<BambooCatalogBrand>? Items);

internal sealed record BambooCatalogBrand(
    [property: JsonPropertyName("internalId")] string? InternalId,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("countryCode")] string? CountryCode,
    [property: JsonPropertyName("currencyCode")] string? CurrencyCode,
    [property: JsonPropertyName("logoUrl")] string? LogoUrl,
    [property: JsonPropertyName("products")] IReadOnlyList<BambooCatalogProduct>? Products);

/// <summary>One orderable denomination within a brand. <see cref="Id"/> is exactly the numeric product id <see cref="BambooHttpClient.PlaceOrderAsync"/> already sends as <c>ProductId</c> — the same identifier space, confirmed by the documented Checkout request shape.</summary>
internal sealed record BambooCatalogProduct(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("minFaceValue")] decimal? MinFaceValue,
    [property: JsonPropertyName("maxFaceValue")] decimal? MaxFaceValue,
    [property: JsonPropertyName("count")] int? Count,
    [property: JsonPropertyName("price")] BambooCatalogPrice? Price);

internal sealed record BambooCatalogPrice(
    [property: JsonPropertyName("min")] decimal? Min,
    [property: JsonPropertyName("max")] decimal? Max,
    [property: JsonPropertyName("currencyCode")] string? CurrencyCode);

/// <summary>
/// Non-secret, per-supplier Bamboo configuration read from <c>Supplier.SettingsJson</c> — the
/// documented funding-source id (<c>AccountId</c> from Get Accounts) to use for purchases. Credentials
/// never live here; they come from <c>Supplier.ApiKey</c>/<c>ApiSecret</c> (encrypted at rest, existing
/// mechanism).
/// </summary>
internal sealed record BambooSupplierSettings(
    [property: JsonPropertyName("accountId")] long? AccountId);
