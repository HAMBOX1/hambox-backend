using System.Net.Http.Json;
using System.Text.Json.Serialization;
using HAMBOX.Application.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HAMBOX.Infrastructure.Currency;

/// <summary>
/// Attempts to load rates from a configurable HTTP API, falling back to static configuration.
/// </summary>
internal sealed class HttpCurrencyExchangeRateProvider(
    HttpClient httpClient,
    IOptions<CurrencySettings> options,
    ConfigurationCurrencyExchangeRateProvider fallbackProvider,
    ILogger<HttpCurrencyExchangeRateProvider> logger)
    : ICurrencyExchangeRateProvider
{
    public async Task<IReadOnlyDictionary<string, decimal>> GetRatesAsync(CancellationToken cancellationToken = default)
    {
        var settings = options.Value;
        var apiUrl = settings.ExternalApiUrl;

        if (string.IsNullOrWhiteSpace(apiUrl))
        {
            return await fallbackProvider.GetRatesAsync(cancellationToken);
        }

        try
        {
            var response = await httpClient.GetFromJsonAsync<ExchangeRateApiResponse>(apiUrl, cancellationToken);
            if (response?.Rates is null || response.Rates.Count == 0)
            {
                return await fallbackProvider.GetRatesAsync(cancellationToken);
            }

            var rates = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
            {
                [settings.BaseCurrency] = 1m,
            };

            foreach (var code in settings.SupportedCurrencies)
            {
                if (response.Rates.TryGetValue(code, out var rate) && rate > 0)
                {
                    rates[code] = rate;
                }
            }

            if (rates.Count <= 1)
            {
                return await fallbackProvider.GetRatesAsync(cancellationToken);
            }

            return rates;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to fetch exchange rates from {ApiUrl}. Using configured fallback rates.", apiUrl);
            return await fallbackProvider.GetRatesAsync(cancellationToken);
        }
    }

    private sealed class ExchangeRateApiResponse
    {
        [JsonPropertyName("rates")]
        public Dictionary<string, decimal>? Rates { get; init; }
    }
}
