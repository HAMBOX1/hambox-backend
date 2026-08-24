using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace HAMBOX.Modules.Commerce.Infrastructure.Services;

internal sealed class CheckoutConfigurationProvider(
    IHostEnvironment environment,
    IOptions<DotSettings> dotOptions,
    IDotPricePointResolver dotPricePointResolver,
    IOptions<DotFawrySettings> dotFawryOptions,
    IDotFawryChargeAmountResolver dotFawryChargeAmountResolver) : ICheckoutConfigurationProvider
{
    public bool IsDevelopmentCheckoutEnabled => environment.IsDevelopment();

    // Settings alone aren't enough to know DOT checkout will actually work — the real signal is
    // whether something other than the default "not configured" stub resolver is registered. See
    // IDotPricePointResolver for the full rationale. Gates the OTP redirect product shared by
    // Orange Cash (opId 117) and Vodafone Cash (opId 114) — see DotWalletOperator; Fawry is a
    // separate product, gated by IsDotFawryCheckoutEnabled below.
    public bool IsDotCheckoutEnabled =>
        dotPricePointResolver is not NotConfiguredDotPricePointResolver
        && !string.IsNullOrWhiteSpace(dotOptions.Value.PartnerId)
        && !string.IsNullOrWhiteSpace(dotOptions.Value.ServiceId)
        && !string.IsNullOrWhiteSpace(dotOptions.Value.PublicRedirectUrl)
        && !string.IsNullOrWhiteSpace(dotOptions.Value.FrontendResultUrl);

    // Same gate, for the separate DOT Fawry Direct Billing product (Fawry only — Orange Cash and
    // Vodafone Cash go through the OTP redirect product above instead, per DOT). The charge
    // currency (EGP) is resolved (DotFawryChargeAmountResolver) — this now just confirms real
    // partner credentials are configured, and still lets ops force-disable Fawry by registering
    // NotConfiguredDotFawryChargeAmountResolver in its place without touching anything else.
    public bool IsDotFawryCheckoutEnabled =>
        dotFawryChargeAmountResolver is not NotConfiguredDotFawryChargeAmountResolver
        && !string.IsNullOrWhiteSpace(dotFawryOptions.Value.PartnerId)
        && !string.IsNullOrWhiteSpace(dotFawryOptions.Value.ServiceId);
}
