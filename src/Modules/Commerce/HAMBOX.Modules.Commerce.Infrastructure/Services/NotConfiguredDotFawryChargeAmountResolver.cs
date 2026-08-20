using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Errors;
using HAMBOX.SharedKernel.Results;

namespace HAMBOX.Modules.Commerce.Infrastructure.Services;

/// <summary>
/// Kill-switch <see cref="IDotFawryChargeAmountResolver"/>: always fails cleanly. Not registered by
/// default now that the currency question is confirmed (see <c>DotFawryChargeAmountResolver</c>) —
/// kept so DOT Fawry checkout can be force-disabled by registering this in place of the real
/// resolver (e.g. ops incident, DOT-side outage) without touching the rest of the wiring;
/// <c>CheckoutConfigurationProvider.IsDotFawryCheckoutEnabled</c> checks for this type specifically.
/// </summary>
internal sealed class NotConfiguredDotFawryChargeAmountResolver : IDotFawryChargeAmountResolver
{
    public Task<Result<DotFawryChargeAmount>> ResolveAsync(
        decimal orderTotalUsd, string countryCode, CancellationToken cancellationToken = default) =>
        Task.FromResult(Result.Failure<DotFawryChargeAmount>(CommerceErrors.DotFawryPricingNotConfigured));
}
