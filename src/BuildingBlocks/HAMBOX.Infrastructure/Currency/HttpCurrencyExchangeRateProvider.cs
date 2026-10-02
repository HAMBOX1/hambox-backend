using System.Net.Http.Json;
using System.Text.Json.Serialization;
using HAMBOX.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HAMBOX.Infrastructure.Currency;

/// <summary>
/// Attempts to load rates from the admin-configured HTTP API (ExternalApiUrl, read live from the
/// Currency Platform Setting — not frozen from <c>appsettings</c> at DI-registration time, same reason
/// as <see cref="DynamicCurrencyExchangeRateProvider"/>'s own live read), falling back to static
/// configuration.
/// </summary>
internal sealed class HttpCurrencyExchangeRateProvider(
    HttpClient httpClient,
    IServiceProvider rootServiceProvider,
    IOptions<CurrencySettings> options,
    ConfigurationCurrencyExchangeRateProvider fallbackProvider,
    ILogger<HttpCurrencyExchangeRateProvider> logger)
    : ICurrencyExchangeRateProvider
{
    public async Task<IReadOnlyDictionary<string, decimal>> GetRatesAsync(CancellationToken cancellationToken = default)
    {
        var settings = await ResolveCurrencySettingsAsync(cancellationToken);
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

            // Every positive rate the API returns is kept, not just the storefront-supported codes:
            // supplier catalogs price in many currencies (AUD, TRY, CAD, ...) and cost normalization
            // needs a live rate for each without an admin having to list them first.
            foreach (var (code, rate) in response.Rates)
            {
                if (rate > 0 && !string.IsNullOrWhiteSpace(code))
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

    private async Task<HAMBOX.Application.PlatformSettings.CurrencySettingsPayload> ResolveCurrencySettingsAsync(
        CancellationToken cancellationToken)
    {
        using var scope = rootServiceProvider.CreateScope();
        var platformSettings = scope.ServiceProvider.GetService<IPlatformSettingsProvider>();
        if (platformSettings is not null)
        {
            return await platformSettings.GetCurrencyAsync(cancellationToken);
        }

        var fallback = options.Value;
        return new HAMBOX.Application.PlatformSettings.CurrencySettingsPayload(
            fallback.BaseCurrency,
            fallback.SupportedCurrencies,
            fallback.RefreshIntervalMinutes,
            fallback.Provider,
            fallback.ExternalApiUrl,
            fallback.StaticRates);
    }

    private sealed class ExchangeRateApiResponse
    {
        [JsonPropertyName("rates")]
        public Dictionary<string, decimal>? Rates { get; init; }
    }
}
