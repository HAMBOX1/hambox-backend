using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using HAMBOX.Modules.Commerce.Application.Options;

namespace HAMBOX.Modules.Commerce.Presentation.Endpoints;

/// <summary>
/// Verifies Cryptomus's webhook <c>sign</c> field: <c>md5(base64(json_without_sign) + ApiKey)</c>.
/// This is a fast first-pass filter only — <c>HandleCryptomusWebhookCommand</c> never trusts the
/// webhook body's own <c>status</c> regardless of a valid signature; it always re-verifies with
/// Cryptomus server-to-server before mutating anything. A mismatch here just skips that re-verify
/// call for an obviously-forged/malformed request rather than being the sole gate.
/// <para>
/// NOTE: the exact byte-for-byte JSON re-encoding Cryptomus's PHP backend used to compute its own
/// signature (property order, escaping) has not been confirmed against a live webhook yet — treat a
/// mismatch here as inconclusive, not proof of forgery, until verified with a real test payment.
/// </para>
/// </summary>
internal static class CryptomusWebhookSignatureVerifier
{
    public static bool IsValid(string rawBody, CryptomusSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            return false;
        }

        try
        {
            var node = JsonNode.Parse(rawBody) as JsonObject;
            if (node is null || !node.TryGetPropertyValue("sign", out var signNode) || signNode is null)
            {
                return false;
            }

            var claimedSign = signNode.GetValue<string>();
            node.Remove("sign");

            var withoutSign = node.ToJsonString();
            var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(withoutSign));
            var expected = Convert.ToHexStringLower(MD5.HashData(Encoding.UTF8.GetBytes(base64 + settings.ApiKey)));

            return string.Equals(expected, claimedSign, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }
}
