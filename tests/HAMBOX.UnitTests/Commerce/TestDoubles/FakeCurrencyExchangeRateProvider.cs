using HAMBOX.Application.Abstractions;

namespace HAMBOX.UnitTests.Commerce.TestDoubles;

/// <summary>Deterministic exchange-rate source for tests — mirrors <c>DotFawryChargeAmountResolverTests.FakeRateProvider</c>'s identical pattern, shared here so multiple test files (routing-engine tests included) don't each redefine it.</summary>
internal sealed class FakeCurrencyExchangeRateProvider : ICurrencyExchangeRateProvider
{
    public IReadOnlyDictionary<string, decimal> Rates { get; set; } =
        new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase) { ["USD"] = 1m, ["EUR"] = 0.92m };

    public Task<IReadOnlyDictionary<string, decimal>> GetRatesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Rates);
}
