using System.Security.Cryptography;
using System.Text;

namespace HAMBOX.Modules.Commerce.Presentation.Endpoints;

/// <summary>
/// Verifies OxaPay's webhook <c>HMAC</c> header: <c>hex(HMACSHA512(rawBody, MerchantApiKey))</c>,
/// per OxaPay's documented webhook security model. Unlike Cryptomus's <c>sign</c> field (embedded in
/// the JSON body itself), OxaPay's signature is a request header computed over the raw, unparsed
/// body — so this must run before any JSON deserialization touches it.
/// <para>
/// This is a fast first-pass filter only — <c>HandleOxaPayWebhookCommand</c> never trusts the
/// webhook body's own <c>status</c> regardless of a valid signature; it always re-verifies with
/// OxaPay server-to-server before mutating anything.
/// </para>
/// </summary>
internal static class OxaPayWebhookSignatureVerifier
{
    public static bool IsValid(string rawBody, string? hmacHeader, string merchantApiKey)
    {
        if (string.IsNullOrWhiteSpace(merchantApiKey) || string.IsNullOrWhiteSpace(hmacHeader))
        {
            return false;
        }

        try
        {
            var expected = Convert.ToHexStringLower(
                HMACSHA512.HashData(Encoding.UTF8.GetBytes(merchantApiKey), Encoding.UTF8.GetBytes(rawBody)));

            return CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(hmacHeader.Trim().ToLowerInvariant()));
        }
        catch
        {
            return false;
        }
    }
}
