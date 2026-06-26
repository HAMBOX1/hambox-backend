using HAMBOX.Application.Abstractions;
using Microsoft.Extensions.Options;

namespace HAMBOX.Infrastructure.Currency;

/// <summary>
/// Reads exchange rates from application configuration.
/// </summary>
internal sealed class ConfigurationCurrencyExchangeRateProvider(IOptions<CurrencySettings> options)
    : ICurrencyExchangeRateProvider
{
    public Task<IReadOnlyDictionary<string, decimal>> GetRatesAsync(CancellationToken cancellationToken = default)
    {
        var settings = options.Value;
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

        return Task.FromResult<IReadOnlyDictionary<string, decimal>>(rates);
    }
}
