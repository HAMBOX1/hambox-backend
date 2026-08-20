using HAMBOX.Infrastructure.Currency;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Errors;
using HAMBOX.SharedKernel.Results;

namespace HAMBOX.Modules.Commerce.Infrastructure.Services;

/// <summary>
/// Confirmed by the client: DOT Fawry charges the EGP equivalent of the order's USD total. Reuses
/// the same <see cref="CurrencyExchangeRateService"/> (and its Platform-Settings-backed
/// USD-per-EGP rate, live or static per the <c>Currency</c> category) that already drives the
/// storefront's display-only currency conversion — this is the one place that conversion result is
/// actually charged, not just displayed. Rounded to 2 decimal places, matching every other example
/// amount in the Direct Billing spec.
/// </summary>
internal sealed class DotFawryChargeAmountResolver(CurrencyExchangeRateService exchangeRateService)
    : IDotFawryChargeAmountResolver
{
    private const string TargetCurrency = "EGP";

    public async Task<Result<DotFawryChargeAmount>> ResolveAsync(
        decimal orderTotalUsd, string countryCode, CancellationToken cancellationToken = default)
    {
        var snapshot = await exchangeRateService.GetRatesAsync(cancellationToken);

        if (!snapshot.Rates.TryGetValue(TargetCurrency, out var usdToEgpRate) || usdToEgpRate <= 0)
        {
            return Result.Failure<DotFawryChargeAmount>(CommerceErrors.DotFawryPricingNotConfigured);
        }

        var amountEgp = Math.Round(orderTotalUsd * usdToEgpRate, 2, MidpointRounding.AwayFromZero);
        return Result.Success(new DotFawryChargeAmount(amountEgp, TargetCurrency));
    }
}
