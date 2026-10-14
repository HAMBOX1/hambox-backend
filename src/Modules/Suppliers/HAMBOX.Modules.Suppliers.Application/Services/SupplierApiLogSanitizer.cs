using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace HAMBOX.Modules.Suppliers.Application.Services;

/// <summary>
/// Makes supplier request / response data safe to store: credentials and anything that can be a delivered code are
/// redacted by field name, query-string secrets are masked, and a successful response is reduced to its identifying
/// fields (audit R40).
/// </summary>
public static partial class SupplierApiLogSanitizer
{
    public const string Redacted = "[redacted]";
    public const string SuccessBodyOmitted = "[omitted: a successful response may contain delivered codes]";

    // Names whose VALUE must never be stored: credentials, and every shape delivered codes arrive in.
    [GeneratedRegex(
        @"^(.*(password|passwd|secret|token|apikey|api_key|api-key|authorization|signature|credential|redeem|voucher|licen[sc]e|cardcode|pincode|activation|serial).*|pin|code|codes|key|keys|cardnumber|cvv)$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SensitiveName();

    // Top-level (or one level down) scalar fields worth keeping from a successful response.
    [GeneratedRegex(
        @"^(id|.*id|status|state|result|message|reference|referenceid|ordernumber|orderstatus|quantity|total|amount|currency|errorcode|error|errors)$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex IdentifyingName();

    [GeneratedRegex(
        @"(?<=[?&])(api[_-]?key|key|token|access_token|secret|signature|sign|password)=[^&]*",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex QuerySecret();

    /// <summary>Scheme + host + path, with secret query-string values masked.</summary>
    public static string SanitizeUrl(Uri? uri, string fallback)
    {
        if (uri is null)
        {
            return fallback;
        }

        var value = uri.IsAbsoluteUri ? uri.GetLeftPart(UriPartial.Path) + uri.Query : uri.OriginalString;
        return QuerySecret().Replace(value, m => m.Value[..(m.Value.IndexOf('=') + 1)] + Redacted);
    }

    /// <summary>Redacts sensitive JSON fields by name; non-JSON bodies are replaced wholesale.</summary>
    public static string? SanitizeBody(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return body;
        }

        try
        {
            var node = JsonNode.Parse(body);
            if (node is null)
            {
                return body;
            }

            RedactNode(node);
            return node.ToJsonString();
        }
        catch (JsonException)
        {
            // Not JSON (HTML error page, form body…): keep a short, secret-free hint rather than raw text.
            return body.Length > 300 ? string.Concat(body.AsSpan(0, 300), "…") : body;
        }
    }

    /// <summary>The identifying fields of a (successful) JSON response as a small JSON object; null if there are none.</summary>
    public static string? Summarise(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        try
        {
            var summary = new JsonObject();
            Collect(JsonNode.Parse(body), summary, depth: 0);
            return summary.Count == 0 ? null : summary.ToJsonString();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static void Collect(JsonNode? node, JsonObject into, int depth)
    {
        if (node is JsonObject obj && depth <= 1)
        {
            foreach (var (name, value) in obj)
            {
                if (SensitiveName().IsMatch(name))
                {
                    continue;
                }

                if (value is JsonValue scalar && IdentifyingName().IsMatch(name))
                {
                    var text = scalar.ToJsonString();
                    if (text.Length <= 120)
                    {
                        into[depth == 0 ? name : $"{name}"] ??= JsonNode.Parse(text);
                    }
                }
                else if (value is JsonObject or JsonArray)
                {
                    Collect(value, into, depth + 1);
                }
            }
        }
        else if (node is JsonArray array && depth <= 1)
        {
            foreach (var item in array.Take(1))
            {
                Collect(item, into, depth + 1);
            }
        }
    }

    private static void RedactNode(JsonNode node)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var name in obj.Select(p => p.Key).ToList())
                {
                    if (SensitiveName().IsMatch(name) && obj[name] is JsonValue)
                    {
                        obj[name] = Redacted;
                    }
                    else if (obj[name] is { } child)
                    {
                        // A sensitive-named array / object is redacted wholesale.
                        if (SensitiveName().IsMatch(name))
                        {
                            obj[name] = Redacted;
                        }
                        else
                        {
                            RedactNode(child);
                        }
                    }
                }

                break;
            case JsonArray array:
                foreach (var item in array)
                {
                    if (item is not null)
                    {
                        RedactNode(item);
                    }
                }

                break;
        }
    }
}
