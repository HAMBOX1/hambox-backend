using HAMBOX.Application.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HAMBOX.Infrastructure.Currency;

/// <summary>
/// Picks the real <see cref="ICurrencyExchangeRateProvider"/> (Http vs Configuration) on every call
/// from the live <c>currency</c> Platform Setting, instead of fixing the choice once at DI-registration
/// time from appsettings — that used to make the admin-editable "Provider" field a no-op. Falls back to
/// appsettings only when Platform Settings is unavailable (e.g. very early startup).
/// </summary>
internal sealed class DynamicCurrencyExchangeRateProvider(
    IServiceProvider rootServiceProvider,
    IConfiguration configuration) : ICurrencyExchangeRateProvider
{
    public async Task<IReadOnlyDictionary<string, decimal>> GetRatesAsync(CancellationToken cancellationToken = default)
    {
        using var scope = rootServiceProvider.CreateScope();
        var platformSettings = scope.ServiceProvider.GetService<IPlatformSettingsProvider>();

        var providerName = platformSettings is not null
            ? (await platformSettings.GetCurrencyAsync(cancellationToken)).Provider
            : configuration.GetSection(CurrencySettings.SectionName).Get<CurrencySettings>()?.Provider;

        ICurrencyExchangeRateProvider provider = string.Equals(providerName, "Http", StringComparison.OrdinalIgnoreCase)
            ? scope.ServiceProvider.GetRequiredService<HttpCurrencyExchangeRateProvider>()
            : scope.ServiceProvider.GetRequiredService<ConfigurationCurrencyExchangeRateProvider>();

        return await provider.GetRatesAsync(cancellationToken);
    }
}
