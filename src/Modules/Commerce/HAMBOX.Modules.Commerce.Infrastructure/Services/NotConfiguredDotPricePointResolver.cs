using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Errors;
using HAMBOX.SharedKernel.Results;

namespace HAMBOX.Modules.Commerce.Infrastructure.Services;

/// <summary>
/// Kill-switch <see cref="IDotPricePointResolver"/>: always fails cleanly. Register this in place of
/// <see cref="DotPricePointResolver"/> to force-disable DOT OTP checkout (Orange Cash/Vodafone Cash)
/// without touching anything else — mirrors <c>NotConfiguredDotFawryChargeAmountResolver</c> for the
/// sibling Direct Billing product.
/// </summary>
internal sealed class NotConfiguredDotPricePointResolver : IDotPricePointResolver
{
    public Task<Result<DotChargeAmount>> ResolveAsync(
        decimal orderTotalUsd, string countryCode, CancellationToken cancellationToken = default) =>
        Task.FromResult(Result.Failure<DotChargeAmount>(CommerceErrors.DotPricingNotConfigured));
}
