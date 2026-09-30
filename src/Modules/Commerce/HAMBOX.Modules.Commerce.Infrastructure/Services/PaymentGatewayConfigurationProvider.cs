using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Options;
using HAMBOX.Modules.Commerce.Domain.PaymentGateways;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace HAMBOX.Modules.Commerce.Infrastructure.Services;

/// <summary>
/// DB-backed, cached implementation of <see cref="IPaymentGatewayConfigurationProvider"/>. Each
/// <c>GetXxxSettingsAsync</c> maps the generic <see cref="PaymentGatewayConfiguration"/> row onto the
/// gateway's existing settings POCO, field-by-field falling back to the legacy
/// <c>IOptions&lt;T&gt;</c> value whenever the DB field is blank — see the interface doc for why.
/// <para>
/// Numeric tuning fields (timeouts, invoice lifetime, DOT's billing paths) are intentionally still
/// sourced from <c>IOptions&lt;T&gt;</c> only, not <see cref="PaymentGatewayConfiguration.AdditionalConfigJson"/>,
/// for this first version — that JSON bag exists for a future gateway that needs it, not because
/// these three do today.
/// </para>
/// </summary>
internal sealed class PaymentGatewayConfigurationProvider(
    ICommerceDbContext dbContext,
    IMemoryCache cache,
    IOptions<CryptomusSettings> cryptomusOptions,
    IOptions<DotSettings> dotOptions,
    IOptions<DotFawrySettings> dotFawryOptions) : IPaymentGatewayConfigurationProvider
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    public async Task<CryptomusSettings> GetCryptomusSettingsAsync(CancellationToken cancellationToken = default)
    {
        var row = await GetRowAsync("cryptomus", cancellationToken);
        var defaults = cryptomusOptions.Value;

        return new CryptomusSettings
        {
            BaseUrl = Coalesce(row?.BaseUrl, defaults.BaseUrl),
            MerchantId = Coalesce(row?.AccountId, defaults.MerchantId),
            ApiKey = Coalesce(row?.ApiKey, defaults.ApiKey),
            PublicWebhookUrl = Coalesce(row?.WebhookUrl, defaults.PublicWebhookUrl),
            FrontendResultUrl = Coalesce(row?.FrontendResultUrl, defaults.FrontendResultUrl),
            InvoiceLifetimeSeconds = defaults.InvoiceLifetimeSeconds,
            RequestTimeoutSeconds = defaults.RequestTimeoutSeconds,
        };
    }

    public async Task<DotSettings> GetDotSettingsAsync(CancellationToken cancellationToken = default)
    {
        var row = await GetRowAsync("dot", cancellationToken);
        var defaults = dotOptions.Value;

        return new DotSettings
        {
            BaseUrl = Coalesce(row?.BaseUrl, defaults.BaseUrl),
            PartnerId = Coalesce(row?.AccountId, defaults.PartnerId),
            ServiceId = Coalesce(row?.SecondaryId, defaults.ServiceId),
            Username = Coalesce(row?.ApiKey, defaults.Username),
            Password = Coalesce(row?.ApiSecret, defaults.Password),
            PublicRedirectUrl = Coalesce(row?.WebhookUrl, defaults.PublicRedirectUrl),
            FrontendResultUrl = Coalesce(row?.FrontendResultUrl, defaults.FrontendResultUrl),
            RequestTimeoutSeconds = defaults.RequestTimeoutSeconds,
        };
    }

    public async Task<DotFawrySettings> GetDotFawrySettingsAsync(CancellationToken cancellationToken = default)
    {
        var row = await GetRowAsync("dotfawry", cancellationToken);
        var defaults = dotFawryOptions.Value;

        return new DotFawrySettings
        {
            BaseUrl = Coalesce(row?.BaseUrl, defaults.BaseUrl),
            DirectBillingPath = defaults.DirectBillingPath,
            CheckTransactionStatusPath = defaults.CheckTransactionStatusPath,
            PartnerId = Coalesce(row?.AccountId, defaults.PartnerId),
            ServiceId = Coalesce(row?.SecondaryId, defaults.ServiceId),
            Username = Coalesce(row?.ApiKey, defaults.Username),
            Password = Coalesce(row?.ApiSecret, defaults.Password),
            RequestTimeoutSeconds = defaults.RequestTimeoutSeconds,
        };
    }

    public async Task<bool> IsEnabledAsync(string gatewayKey, CancellationToken cancellationToken = default)
    {
        var row = await GetRowAsync(gatewayKey, cancellationToken);
        return row?.IsEnabled ?? false;
    }

    public async Task<decimal?> GetFeePercentAsync(string gatewayKey, CancellationToken cancellationToken = default)
    {
        var row = await GetRowAsync(gatewayKey, cancellationToken);
        return row?.FeePercent;
    }

    public void InvalidateCache(string gatewayKey) => cache.Remove(CacheKey(gatewayKey));

    private async Task<PaymentGatewayConfiguration?> GetRowAsync(string gatewayKey, CancellationToken cancellationToken)
    {
        if (cache.TryGetValue(CacheKey(gatewayKey), out PaymentGatewayConfiguration? cached))
        {
            return cached;
        }

        var row = await dbContext.PaymentGatewayConfigurations
            .AsNoTracking()
            .FirstOrDefaultAsync(g => g.GatewayKey == gatewayKey, cancellationToken);

        cache.Set(CacheKey(gatewayKey), row, CacheTtl);
        return row;
    }

    private static string CacheKey(string gatewayKey) => $"PaymentGatewayConfiguration:{gatewayKey}";

    private static string Coalesce(string? dbValue, string fallback) =>
        string.IsNullOrWhiteSpace(dbValue) ? fallback : dbValue;
}
