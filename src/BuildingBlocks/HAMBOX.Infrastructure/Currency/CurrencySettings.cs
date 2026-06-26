namespace HAMBOX.Infrastructure.Currency;

/// <summary>
/// Configuration for currency conversion and exchange-rate refresh.
/// </summary>
public sealed class CurrencySettings
{
    public const string SectionName = "Currency";

    /// <summary>ISO 4217 code for stored database prices.</summary>
    public string BaseCurrency { get; set; } = "USD";

    /// <summary>How often to refresh rates in minutes.</summary>
    public int RefreshIntervalMinutes { get; set; } = 60;

    /// <summary>Provider name: Configuration or Http.</summary>
    public string Provider { get; set; } = "Configuration";

    /// <summary>Optional external rates API URL (expects JSON with USD base rates).</summary>
    public string? ExternalApiUrl { get; set; }

    /// <summary>Fallback/static rates (units per 1 USD).</summary>
    public Dictionary<string, decimal> StaticRates { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["USD"] = 1m,
        ["EUR"] = 0.92m,
        ["EGP"] = 48.50m,
        ["SAR"] = 3.75m,
    };

    public string[] SupportedCurrencies { get; set; } = ["USD", "EUR", "EGP", "SAR"];
}
