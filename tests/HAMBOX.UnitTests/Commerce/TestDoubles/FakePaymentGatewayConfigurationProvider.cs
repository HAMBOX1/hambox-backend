using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Options;

namespace HAMBOX.UnitTests.Commerce.TestDoubles;

/// <summary>
/// Deterministic <see cref="IPaymentGatewayConfigurationProvider"/> for handler tests: every gateway is
/// enabled, carries no fee override (so the platform default tax rate applies) and returns default settings.
/// </summary>
internal sealed class FakePaymentGatewayConfigurationProvider : IPaymentGatewayConfigurationProvider
{
    public decimal? FeePercent { get; set; }

    public Task<CryptomusSettings> GetCryptomusSettingsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new CryptomusSettings());

    public Task<DotSettings> GetDotSettingsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new DotSettings());

    public Task<DotFawrySettings> GetDotFawrySettingsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new DotFawrySettings());

    public Task<OxaPaySettings> GetOxaPaySettingsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new OxaPaySettings());

    public Task<bool> IsEnabledAsync(string gatewayKey, CancellationToken cancellationToken = default) =>
        Task.FromResult(true);

    public Task<decimal?> GetFeePercentAsync(string gatewayKey, CancellationToken cancellationToken = default) =>
        Task.FromResult(FeePercent);

    public void InvalidateCache(string gatewayKey)
    {
    }
}
