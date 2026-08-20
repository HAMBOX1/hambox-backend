using HAMBOX.Application.Abstractions;
using HAMBOX.Infrastructure.Currency;
using HAMBOX.Modules.Commerce.Application.Errors;
using HAMBOX.Modules.Commerce.Infrastructure.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace HAMBOX.UnitTests.Commerce.DotFawry;

public sealed class DotFawryChargeAmountResolverTests
{
    private sealed class FakeRateProvider : ICurrencyExchangeRateProvider
    {
        public IReadOnlyDictionary<string, decimal> Rates { get; set; } =
            new Dictionary<string, decimal> { ["USD"] = 1m, ["EGP"] = 48.50m };

        public Task<IReadOnlyDictionary<string, decimal>> GetRatesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(Rates);
    }

    private static (DotFawryChargeAmountResolver Resolver, FakeRateProvider RateProvider) CreateResolver(
        CurrencySettings? currencySettings = null)
    {
        var rateProvider = new FakeRateProvider();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var settings = Options.Create(currencySettings ?? new CurrencySettings());
        var scopeFactory = new ServiceCollection().BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();

        var service = new CurrencyExchangeRateService(rateProvider, cache, settings, TimeProvider.System, scopeFactory);
        return (new DotFawryChargeAmountResolver(service), rateProvider);
    }

    [Fact]
    public async Task ResolveAsync_ConvertsUsdTotalToEgpEquivalent()
    {
        var (resolver, _) = CreateResolver();

        var result = await resolver.ResolveAsync(10m, "EG", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("EGP", result.Value.Currency);
        Assert.Equal(485.00m, result.Value.Amount);
    }

    [Fact]
    public async Task ResolveAsync_RoundsToTwoDecimalPlaces()
    {
        var (resolver, rateProvider) = CreateResolver();
        rateProvider.Rates = new Dictionary<string, decimal> { ["USD"] = 1m, ["EGP"] = 48.567m };

        var result = await resolver.ResolveAsync(1m, "EG", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(48.57m, result.Value.Amount);
    }

    [Fact]
    public async Task ResolveAsync_MissingEgpRate_ReturnsPricingNotConfigured()
    {
        // No static EGP fallback either — SupportedCurrencies excludes it entirely, so the
        // normalized snapshot never contains an "EGP" key regardless of what the live provider returns.
        var currencySettings = new CurrencySettings { SupportedCurrencies = ["USD"] };
        var (resolver, rateProvider) = CreateResolver(currencySettings);
        rateProvider.Rates = new Dictionary<string, decimal> { ["USD"] = 1m };

        var result = await resolver.ResolveAsync(10m, "EG", CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(CommerceErrors.DotFawryPricingNotConfigured.Code, result.Error.Code);
    }
}
