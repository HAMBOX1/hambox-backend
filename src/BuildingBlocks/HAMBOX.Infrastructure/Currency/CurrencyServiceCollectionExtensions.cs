using HAMBOX.Application.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HAMBOX.Infrastructure.Currency;

/// <summary>
/// Registers currency conversion services.
/// </summary>
public static class CurrencyServiceCollectionExtensions
{
    /// <summary>Adds currency exchange-rate services.</summary>
    public static IServiceCollection AddHamboxCurrency(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<CurrencySettings>(configuration.GetSection(CurrencySettings.SectionName));
        services.AddMemoryCache();
        services.AddSingleton<ConfigurationCurrencyExchangeRateProvider>();
        services.AddHttpClient<HttpCurrencyExchangeRateProvider>();

        services.AddSingleton<ICurrencyExchangeRateProvider>(sp =>
        {
            var settings = configuration.GetSection(CurrencySettings.SectionName).Get<CurrencySettings>()
                ?? new CurrencySettings();

            return string.Equals(settings.Provider, "Http", StringComparison.OrdinalIgnoreCase)
                ? sp.GetRequiredService<HttpCurrencyExchangeRateProvider>()
                : sp.GetRequiredService<ConfigurationCurrencyExchangeRateProvider>();
        });

        services.AddSingleton<CurrencyExchangeRateService>();
        return services;
    }
}
