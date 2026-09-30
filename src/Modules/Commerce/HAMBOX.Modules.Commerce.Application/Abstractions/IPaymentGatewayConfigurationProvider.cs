using HAMBOX.Modules.Commerce.Application.Options;

namespace HAMBOX.Modules.Commerce.Application.Abstractions;

/// <summary>
/// Resolves each gateway's settings from the admin-managed <c>PaymentGatewayConfiguration</c> table
/// (cached — see the Infrastructure implementation), falling back field-by-field to the legacy
/// <c>IOptions&lt;T&gt;</c>/appsettings value when the DB row hasn't been filled in yet. This is what
/// lets rollout happen gateway-by-gateway without a "flag day" — an unconfigured DB row is
/// transparently invisible to callers.
/// </summary>
public interface IPaymentGatewayConfigurationProvider
{
    Task<CryptomusSettings> GetCryptomusSettingsAsync(CancellationToken cancellationToken = default);

    Task<DotSettings> GetDotSettingsAsync(CancellationToken cancellationToken = default);

    Task<DotFawrySettings> GetDotFawrySettingsAsync(CancellationToken cancellationToken = default);

    /// <summary>True only when an admin has explicitly enabled this gateway's DB row (defaults to false for an unconfigured/missing row).</summary>
    Task<bool> IsEnabledAsync(string gatewayKey, CancellationToken cancellationToken = default);

    /// <summary>The gateway's own fee/tax percentage (0-100), or null when unconfigured — callers should fall back to the platform default rate in that case.</summary>
    Task<decimal?> GetFeePercentAsync(string gatewayKey, CancellationToken cancellationToken = default);

    /// <summary>Call after any admin write to a gateway's row so the next read reflects it immediately instead of waiting out the cache TTL.</summary>
    void InvalidateCache(string gatewayKey);
}
