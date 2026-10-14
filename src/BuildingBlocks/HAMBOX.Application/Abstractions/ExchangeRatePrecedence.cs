namespace HAMBOX.Application.Abstractions;

/// <summary>
/// The single rule for which exchange rate wins — used by checkout display conversion AND the supplier-mapping
/// cost view, so the two can never disagree (audit R34).
/// <list type="number">
/// <item>An admin override: a row in the Currency settings' static table that the admin actually chose.</item>
/// <item>Otherwise the live (or configured-provider) rate.</item>
/// <item>Otherwise the static table's value as a last resort, so a provider outage never leaves a currency unpriced.</item>
/// </list>
/// A static row whose value equals the platform's originally seeded default (USD 1, EUR 0.92, EGP 48.50, SAR 3.75) is
/// NOT treated as an override: those numbers were never a decision, and silently pinning them above live rates is
/// exactly the surprise this rule exists to remove.
/// </summary>
public static class ExchangeRatePrecedence
{
    private static readonly IReadOnlyDictionary<string, decimal> SeededDefaults =
        new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            ["USD"] = 1m,
            ["EUR"] = 0.92m,
            ["EGP"] = 48.50m,
            ["SAR"] = 3.75m,
        };

    /// <summary>True when the static row is a deliberate admin value rather than the seeded default.</summary>
    public static bool IsAdminOverride(string currencyCode, decimal staticRate) =>
        staticRate > 0
        && !(SeededDefaults.TryGetValue(currencyCode, out var seeded) && seeded == staticRate);

    /// <summary>Resolves one rate per requested code; codes with no usable rate anywhere are left out.</summary>
    public static Dictionary<string, decimal> Resolve(
        IEnumerable<string> codes,
        IReadOnlyDictionary<string, decimal> liveRates,
        IReadOnlyDictionary<string, decimal> staticRates)
    {
        var resolved = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

        foreach (var code in codes)
        {
            if (staticRates.TryGetValue(code, out var staticRate) && IsAdminOverride(code, staticRate))
            {
                resolved[code] = staticRate;
            }
            else if (liveRates.TryGetValue(code, out var live) && live > 0)
            {
                resolved[code] = live;
            }
            else if (staticRates.TryGetValue(code, out var fallback) && fallback > 0)
            {
                resolved[code] = fallback;
            }
        }

        return resolved;
    }
}
