using HAMBOX.Modules.Commerce.Application.Abstractions;
using Microsoft.Extensions.Hosting;

namespace HAMBOX.Modules.Commerce.Infrastructure.Services;

internal sealed class CheckoutConfigurationProvider(
    IHostEnvironment environment,
    IPaymentGatewayConfigurationProvider gatewaySettings,
    IDotPricePointResolver dotPricePointResolver,
    IDotFawryChargeAmountResolver dotFawryChargeAmountResolver) : ICheckoutConfigurationProvider
{
    public bool IsDevelopmentCheckoutEnabled => environment.IsDevelopment();

    // A gateway must both be enabled by an admin on the dashboard AND have the format-level
    // prerequisites a checkout attempt actually needs. Settings alone were never enough to know DOT
    // checkout will work — the real signal is whether something other than the default "not
    // configured" stub resolver is registered. See IDotPricePointResolver for the full rationale.
    // Gates the OTP redirect product shared by Orange Cash (opId 117) and Vodafone Cash (opId 114) —
    // see DotWalletOperator; Fawry is a separate product, gated by IsDotFawryCheckoutEnabled below.
    public bool IsDotCheckoutEnabled => CheckAsync(async () =>
    {
        if (!await gatewaySettings.IsEnabledAsync("dot"))
        {
            return false;
        }

        var settings = await gatewaySettings.GetDotSettingsAsync();
        return dotPricePointResolver is not NotConfiguredDotPricePointResolver
            && !string.IsNullOrWhiteSpace(settings.PartnerId)
            && !string.IsNullOrWhiteSpace(settings.ServiceId)
            && !string.IsNullOrWhiteSpace(settings.PublicRedirectUrl)
            && !string.IsNullOrWhiteSpace(settings.FrontendResultUrl);
    });

    // Same gate, for the separate DOT Fawry Direct Billing product (Fawry only — Orange Cash and
    // Vodafone Cash go through the OTP redirect product above instead, per DOT). The charge
    // currency (EGP) is resolved (DotFawryChargeAmountResolver) — this now just confirms real
    // partner credentials are configured, and still lets ops force-disable Fawry by registering
    // NotConfiguredDotFawryChargeAmountResolver in its place without touching anything else.
    public bool IsDotFawryCheckoutEnabled => CheckAsync(async () =>
    {
        if (!await gatewaySettings.IsEnabledAsync("dotfawry"))
        {
            return false;
        }

        var settings = await gatewaySettings.GetDotFawrySettingsAsync();
        return dotFawryChargeAmountResolver is not NotConfiguredDotFawryChargeAmountResolver
            && !string.IsNullOrWhiteSpace(settings.PartnerId)
            && !string.IsNullOrWhiteSpace(settings.ServiceId);
    });

    // No price-point-resolver analog needed (Cryptomus invoices directly in USD — see
    // CryptomusPaymentGateway), so admin-enabled + settings presence is the whole gate.
    public bool IsCryptomusCheckoutEnabled => CheckAsync(async () =>
    {
        if (!await gatewaySettings.IsEnabledAsync("cryptomus"))
        {
            return false;
        }

        var settings = await gatewaySettings.GetCryptomusSettingsAsync();
        return !string.IsNullOrWhiteSpace(settings.MerchantId)
            && !string.IsNullOrWhiteSpace(settings.ApiKey)
            && !string.IsNullOrWhiteSpace(settings.PublicWebhookUrl);
    });

    // ICheckoutConfigurationProvider's members are synchronous properties (read by
    // GetCheckoutConfigurationQueryHandler and elsewhere as plain bools) but resolving admin-managed
    // settings now requires an (in-memory-cached, sub-millisecond in the common case) async DB read.
    // Blocking here is the least invasive way to keep that synchronous contract rather than changing
    // every caller across the module for what is, after the first cache fill, a cache hit.
    private static bool CheckAsync(Func<Task<bool>> check) => check().GetAwaiter().GetResult();
}
