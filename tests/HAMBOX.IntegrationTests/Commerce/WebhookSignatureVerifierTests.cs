using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using HAMBOX.Modules.Commerce.Application.Options;
using HAMBOX.Modules.Commerce.Presentation.Endpoints;

namespace HAMBOX.IntegrationTests.Commerce;

/// <summary>
/// Audit R30: the first tests of the two crypto gateways' webhook signature checks — what a genuine, signed
/// callback looks like, and every way a forged or malformed one must be refused.
/// </summary>
public sealed class WebhookSignatureVerifierTests
{
    private const string ApiKey = "test-api-key-not-a-real-secret";

    private static CryptomusSettings Settings(string? key = ApiKey) => new() { ApiKey = key ?? string.Empty };

    private static string Md5Sign(string json) =>
        Convert.ToHexStringLower(MD5.HashData(Encoding.UTF8.GetBytes(Convert.ToBase64String(Encoding.UTF8.GetBytes(json)) + ApiKey)));

    private static string WithSign(string jsonWithoutSign, string sign)
    {
        var node = JsonNode.Parse(jsonWithoutSign)!.AsObject();
        node["sign"] = sign;
        return node.ToJsonString(new System.Text.Json.JsonSerializerOptions
        {
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        });
    }

    // ─── Cryptomus ──────────────────────────────────────────────────────

    [Fact]
    public void Cryptomus_AcceptsAGenuinelySignedBody()
    {
        const string payload = """{"order_id":"HB-1","uuid":"u-1","status":"paid","is_final":true}""";
        var body = WithSign(payload, Md5Sign(payload));

        Assert.True(CryptomusWebhookSignatureVerifier.IsValid(body, Settings()));
    }

    [Fact]
    public void Cryptomus_AcceptsPhpStyleEscapingOfSlashes()
    {
        // PHP's json_encode writes "/" as "\/" — that is the form Cryptomus signed.
        const string signedForm = """{"order_id":"HB\/1","url":"https:\/\/pay.example\/x","status":"paid"}""";
        var body = WithSign("""{"order_id":"HB/1","url":"https://pay.example/x","status":"paid"}""", Md5Sign(signedForm));

        Assert.True(CryptomusWebhookSignatureVerifier.IsValid(body, Settings()));
    }

    [Fact]
    public void Cryptomus_AcceptsBodiesWithAmpersandsPlusSignsAndNonAsciiText()
    {
        // System.Text.Json's default encoder would turn these into & / + / ا…, PHP leaves them as-is.
        const string payload = """{"order_id":"HB-2","note":"a&b+c ' عميل","status":"paid"}""";
        var body = WithSign(payload, Md5Sign(payload));

        Assert.True(CryptomusWebhookSignatureVerifier.IsValid(body, Settings()));
    }

    [Fact]
    public void Cryptomus_RejectsATamperedBody()
    {
        const string payload = """{"order_id":"HB-1","uuid":"u-1","status":"paid","is_final":true}""";
        var genuine = WithSign(payload, Md5Sign(payload));
        var tampered = genuine.Replace("\"order_id\":\"HB-1\"", "\"order_id\":\"HB-999\"");

        Assert.False(CryptomusWebhookSignatureVerifier.IsValid(tampered, Settings()));
    }

    [Fact]
    public void Cryptomus_RejectsASignatureMadeWithAnotherKey()
    {
        const string payload = """{"order_id":"HB-1","status":"paid"}""";
        var body = WithSign(payload, Md5Sign(payload));

        Assert.False(CryptomusWebhookSignatureVerifier.IsValid(body, Settings("a-different-key")));
    }

    [Theory]
    [InlineData("""{"order_id":"HB-1","status":"paid"}""")]          // no sign at all
    [InlineData("""{"order_id":"HB-1","sign":"","status":"paid"}""")]  // empty sign
    [InlineData("not json")]
    [InlineData("")]
    [InlineData("[1,2,3]")]
    public void Cryptomus_RejectsMissingOrMalformedInput(string body) =>
        Assert.False(CryptomusWebhookSignatureVerifier.IsValid(body, Settings()));

    [Fact]
    public void Cryptomus_RejectsEverythingWhenNoApiKeyIsConfigured()
    {
        const string payload = """{"order_id":"HB-1","status":"paid"}""";
        var body = WithSign(payload, Md5Sign(payload));

        Assert.False(CryptomusWebhookSignatureVerifier.IsValid(body, Settings(string.Empty)));
    }

    // ─── OxaPay ─────────────────────────────────────────────────────────

    private static string HmacOf(string body, string key) =>
        Convert.ToHexStringLower(HMACSHA512.HashData(Encoding.UTF8.GetBytes(key), Encoding.UTF8.GetBytes(body)));

    [Fact]
    public void OxaPay_AcceptsTheHmacOfTheRawBody_CaseInsensitively()
    {
        const string body = """{"track_id":"t-1","order_id":"HB-1","status":"paid"}""";
        var hmac = HmacOf(body, "merchant-key");

        Assert.True(OxaPayWebhookSignatureVerifier.IsValid(body, hmac, "merchant-key"));
        Assert.True(OxaPayWebhookSignatureVerifier.IsValid(body, hmac.ToUpperInvariant(), "merchant-key"));
        Assert.True(OxaPayWebhookSignatureVerifier.IsValid(body, "  " + hmac + " ", "merchant-key"));
    }

    [Fact]
    public void OxaPay_RejectsAChangedBody_ADifferentKey_AndMissingPieces()
    {
        const string body = """{"track_id":"t-1","order_id":"HB-1","status":"paid"}""";
        var hmac = HmacOf(body, "merchant-key");

        Assert.False(OxaPayWebhookSignatureVerifier.IsValid(body.Replace("paid", "fail"), hmac, "merchant-key"));
        Assert.False(OxaPayWebhookSignatureVerifier.IsValid(body, hmac, "other-key"));
        Assert.False(OxaPayWebhookSignatureVerifier.IsValid(body, null, "merchant-key"));
        Assert.False(OxaPayWebhookSignatureVerifier.IsValid(body, "", "merchant-key"));
        Assert.False(OxaPayWebhookSignatureVerifier.IsValid(body, hmac, ""));
        Assert.False(OxaPayWebhookSignatureVerifier.IsValid(body, "not-hex-at-all", "merchant-key"));
    }
}
