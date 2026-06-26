using HAMBOX.Application.Abstractions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace HAMBOX.Infrastructure.Currency;

/// <summary>
/// Caches exchange rates and serves supported currency metadata.
/// </summary>
public sealed class CurrencyExchangeRateService(
    ICurrencyExchangeRateProvider provider,
    IMemoryCache cache,
    IOptions<CurrencySettings> options,
    TimeProvider timeProvider)
{
    private const string CacheKey = "hambox.exchange-rates";

    /// <summary>Gets supported currency codes.</summary>
    public IReadOnlyList<string> SupportedCurrencies => options.Value.SupportedCurrencies;

    /// <summary>Gets the base currency used for stored prices.</summary>
    public string BaseCurrency => options.Value.BaseCurrency;

    /// <summary>Returns cached rates or refreshes when stale.</summary>
    public async Task<CurrencyRatesSnapshot> GetRatesAsync(CancellationToken cancellationToken = default)
    {
        if (cache.TryGetValue(CacheKey, out CurrencyRatesSnapshot? cached) && cached is not null)
        {
            return cached;
        }

        return await RefreshRatesAsync(cancellationToken);
    }

    /// <summary>Forces a refresh from the configured provider.</summary>
    public async Task<CurrencyRatesSnapshot> RefreshRatesAsync(CancellationToken cancellationToken = default)
    {
        var settings = options.Value;
        var fetched = await provider.GetRatesAsync(cancellationToken);
        var rates = NormalizeRates(fetched, settings);

        var snapshot = new CurrencyRatesSnapshot(
            settings.BaseCurrency,
            rates,
            timeProvider.GetUtcNow());

        cache.Set(
            CacheKey,
            snapshot,
            TimeSpan.FromMinutes(Math.Max(5, settings.RefreshIntervalMinutes)));

        return snapshot;
    }

    private static IReadOnlyDictionary<string, decimal> NormalizeRates(
        IReadOnlyDictionary<string, decimal> fetched,
        CurrencySettings settings)
    {
        var normalized = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

        foreach (var code in settings.SupportedCurrencies)
        {
            if (fetched.TryGetValue(code, out var rate) && rate > 0)
            {
                normalized[code] = rate;
                continue;
            }

            if (settings.StaticRates.TryGetValue(code, out var fallback) && fallback > 0)
            {
                normalized[code] = fallback;
            }
        }

        if (!normalized.ContainsKey(settings.BaseCurrency))
        {
            normalized[settings.BaseCurrency] = 1m;
        }

        return normalized;
    }
}

/// <summary>Cached exchange-rate payload.</summary>
/// <param name="BaseCurrency">Stored price currency.</param>
/// <param name="Rates">Units per one base currency.</param>
/// <param name="UpdatedAtUtc">When rates were last refreshed.</param>
public sealed record CurrencyRatesSnapshot(
    string BaseCurrency,
    IReadOnlyDictionary<string, decimal> Rates,
    DateTimeOffset UpdatedAtUtc);
