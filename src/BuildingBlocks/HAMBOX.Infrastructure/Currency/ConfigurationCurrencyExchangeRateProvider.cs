using HAMBOX.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace HAMBOX.Infrastructure.Currency;

/// <summary>
/// Reads exchange rates from the live, admin-editable Currency Platform Setting (StaticRates,
/// SupportedCurrencies, BaseCurrency) — falling back to <c>appsettings</c> only when Platform Settings
/// is unavailable (very early startup), same fallback rule as <see cref="DynamicCurrencyExchangeRateProvider"/>.
/// This class is registered as a singleton, so it resolves the scoped <see cref="IPlatformSettingsProvider"/>
/// through a fresh DI scope per call rather than taking it as a constructor dependency (captive
/// dependency).
/// </summary>
internal sealed class ConfigurationCurrencyExchangeRateProvider(
    IServiceProvider rootServiceProvider,
    IOptions<CurrencySettings> options) : ICurrencyExchangeRateProvider
{
    public async Task<IReadOnlyDictionary<string, decimal>> GetRatesAsync(CancellationToken cancellationToken = default)
    {
        var settings = await ResolveCurrencySettingsAsync(cancellationToken);
        var rates = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

        foreach (var code in settings.SupportedCurrencies)
        {
            if (settings.StaticRates.TryGetValue(code, out var rate) && rate > 0)
            {
                rates[code] = rate;
            }
        }

        if (!rates.ContainsKey(settings.BaseCurrency))
        {
            rates[settings.BaseCurrency] = 1m;
        }

        return rates;
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
}
