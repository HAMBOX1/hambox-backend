using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HAMBOX.Modules.Suppliers.Infrastructure.Providers.GlobeTopper;

/// <summary>
/// GlobeTopper formats larger denomination amounts with a thousands-separator comma inside the JSON
/// string (e.g. <c>"max": "1,000.00"</c>) — confirmed against a real sandbox product ("Amazon UAE",
/// <c>min: "100.00"</c>, <c>max: "1,000.00"</c>) that otherwise broke deserialization entirely (a plain
/// <c>decimal?</c> property, even with <c>JsonNumberHandling.AllowReadingFromString</c>, rejects the comma
/// and throws). Never documented anywhere in the OpenAPI document. The comma is stripped before parsing —
/// nothing else about the string is altered or guessed.
/// </summary>
internal sealed class GlobeTopperFlexibleDecimalConverter : JsonConverter<decimal?>
{
    public override decimal? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        if (reader.TokenType == JsonTokenType.Number)
        {
            return reader.GetDecimal();
        }

        if (reader.TokenType == JsonTokenType.String)
        {
            var raw = reader.GetString();
            if (string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }

            var normalized = raw.Replace(",", string.Empty).Trim();
            if (decimal.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                return value;
            }

            throw new JsonException($"GlobeTopper returned an unparsable decimal value: '{raw}'.");
        }

        throw new JsonException($"Unexpected token type {reader.TokenType} for a GlobeTopper decimal field.");
    }

    public override void Write(Utf8JsonWriter writer, decimal? value, JsonSerializerOptions options)
    {
        if (value is decimal v)
        {
            writer.WriteNumberValue(v);
        }
        else
        {
            writer.WriteNullValue();
        }
    }
}

/// <summary>
/// Every GlobeTopper-specific wire type lives in this one file, isolated inside the provider boundary —
/// nothing outside <c>Providers/GlobeTopper/</c> ever references these. Shapes are transcribed from the
/// live OpenAPI document (<c>https://partner.globetopper.com/api/v2/docs/schema</c>) and confirmed
/// against real sandbox responses — see docs/integrations/suppliers/README.md.
/// </summary>
/// <remarks>
/// GlobeTopper wraps every response — success or business failure — in this one envelope shape, always
/// under HTTP 200 for the endpoints that can fail in-band (confirmed: the Purchase endpoint's documented
/// examples for out-of-stock/insufficient-balance/access-denied are all shown under a single HTTP 200
/// response definition, never a distinct 4xx). <see cref="ResponseCode"/> is the real outcome signal for
/// those endpoints, not the HTTP status. Confirmed present (as <c>"responseCode"</c>, value <c>200</c>)
/// on real GET responses too (<c>/user</c>, <c>/country/search-countries</c>,
/// <c>/product/search-all-products</c>) during live sandbox verification — even though the abstract
/// OpenAPI schema for those GET endpoints omits the field and the Transaction endpoints' abstract schema
/// names it <c>"response"</c> instead of <c>"responseCode"</c>. Real, observed behavior is trusted over
/// the (self-inconsistent) abstract schema here, per the same "verify against real calls" lesson learned
/// integrating Bamboo (see that provider's README section).
/// </remarks>
internal sealed record GlobeTopperEnvelope<TRecord>(
    [property: JsonPropertyName("totalRecords")] int TotalRecords,
    [property: JsonPropertyName("responseCode")] int? ResponseCode,
    [property: JsonPropertyName("responseMessage")] string? ResponseMessage,
    [property: JsonPropertyName("records")] IReadOnlyList<TRecord>? Records);

internal sealed record GlobeTopperCurrencyRef(
    [property: JsonPropertyName("code")] string? Code,
    [property: JsonPropertyName("name")] string? Name);

/// <summary>
/// Only the safe, non-financial, non-PII fields from <c>GET /user</c> — never
/// <c>available_credit_usd</c>/<c>available_credit_local</c> (financial detail, matches
/// <c>BambooSupplierProvider</c>/<c>VisoriaSupplierProvider</c>'s identical "no balance in the summary"
/// convention) and never <c>email</c>/<c>phone</c>/<c>address</c> (PII beyond what a connection-test
/// summary needs).
/// </summary>
internal sealed record GlobeTopperUser(
    [property: JsonPropertyName("agent_id")] long AgentId,
    [property: JsonPropertyName("currency")] GlobeTopperCurrencyRef? Currency);

internal sealed record GlobeTopperOperator(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("name")] string? Name);

internal sealed record GlobeTopperCategory(
    [property: JsonPropertyName("name")] string? Name);

/// <summary>
/// One denomination/product record from <c>GET /product/search-all-products</c> ("TopupValueRef" in the
/// OpenAPI document). <see cref="Operator"/>'s <c>id</c> — NOT <see cref="BillerId"/> — is the id
/// <c>POST /transaction/do-by-product/{{productID}}/{{amount}}</c> actually takes, per the documented
/// cross-reference ("The Product ID defined by <c>operator -&gt; id</c> in
/// <c>/product/search-all-products</c>"). Confirmed against a real sandbox response: <see cref="Min"/>/
/// <see cref="Max"/> arrive as JSON strings (e.g. <c>"1.00"</c>), not numbers — and, for larger amounts,
/// with a thousands-separator comma (e.g. <c>"1,000.00"</c>) that plain <c>decimal?</c> parsing rejects;
/// see <see cref="GlobeTopperFlexibleDecimalConverter"/>, applied here for exactly that reason.
/// </summary>
internal sealed record GlobeTopperProduct(
    [property: JsonPropertyName("BillerID")] long? BillerId,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("currency")] GlobeTopperCurrencyRef? Currency,
    [property: JsonPropertyName("operator")] GlobeTopperOperator? Operator,
    [property: JsonPropertyName("min"), JsonConverter(typeof(GlobeTopperFlexibleDecimalConverter))] decimal? Min,
    [property: JsonPropertyName("max"), JsonConverter(typeof(GlobeTopperFlexibleDecimalConverter))] decimal? Max,
    [property: JsonPropertyName("category")] GlobeTopperCategory? Category);

/// <summary>
/// One transaction record ("TopupTransaction" in the OpenAPI document) — returned both by a successful
/// Purchase call and by <c>GET /transaction/search-transactions/{{transactionID}}</c>.
/// <see cref="ExtraFields"/> is GlobeTopper's free-form, per-product-varying set of delivered redemption
/// attributes (Pin Number, Claim Code, Redemption URL, Barcode Number, Security Code, ...) — there is no
/// single fixed "the code" field documented, so every non-empty entry is treated as part of the delivered
/// payload rather than guessing which one is "the" code (see
/// <c>GlobeTopperSupplierProvider.ExtractDeliveredCodes</c>). Values are <see cref="JsonElement"/> because
/// the documented shape allows either a string or an array of strings per key.
/// </summary>
internal sealed record GlobeTopperTransaction(
    [property: JsonPropertyName("trans_id")] long TransId,
    [property: JsonPropertyName("status_description")] string? StatusDescription,
    [property: JsonPropertyName("extra_fields")] Dictionary<string, JsonElement>? ExtraFields);

/// <summary>Only the two outcomes actually confirmed/documented — see <c>GlobeTopperSupplierProvider.MapTransactionStatus</c>'s remarks for why every other string maps to <c>Unknown</c> rather than being guessed.</summary>
internal static class GlobeTopperStatusDescription
{
    public const string Success = "Success";
    public const string Failed = "Failed";
}

/// <summary>
/// Documented <c>responseCode</c> values for the Purchase endpoint's in-band failure examples. Any
/// value not in this list (besides <c>200</c> for success) maps to <c>UnknownProviderState</c> — never
/// guessed.
/// </summary>
internal static class GlobeTopperResponseCode
{
    public const int Success = 200;
    public const int InternalError = 0;
    public const int TransactionFailed = 2;
    public const int OutOfStock = 202;
    public const int ProductUnavailable = 204;
    public const int AccountBalanceInsufficientMaster = 211;
    public const int AccountBalanceInsufficient = 212;
    public const int AccessDeniedAccountBlocked = 301;
    public const int AccessDeniedInvalidIp = 311;
}
