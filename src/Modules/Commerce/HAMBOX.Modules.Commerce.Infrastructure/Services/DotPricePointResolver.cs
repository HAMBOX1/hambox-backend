using HAMBOX.Infrastructure.Currency;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Errors;
using HAMBOX.SharedKernel.Results;

namespace HAMBOX.Modules.Commerce.Infrastructure.Services;

/// <summary>
/// Charges the EGP equivalent of the order's USD total — same conversion
/// <see cref="DotFawryChargeAmountResolver"/> already uses for the sibling Direct Billing product,
/// since both Orange Cash (op_id 117) and Vodafone Cash (op_id 114) are Egyptian wallets.
/// </summary>
internal sealed class DotPricePointResolver(CurrencyExchangeRateService exchangeRateService)
    : IDotPricePointResolver
{
    private const string TargetCurrency = "EGP";

    public async Task<Result<DotChargeAmount>> ResolveAsync(
        decimal orderTotalUsd, string countryCode, CancellationToken cancellationToken = default)
    {
        var snapshot = await exchangeRateService.GetRatesAsync(cancellationToken);

        if (!snapshot.Rates.TryGetValue(TargetCurrency, out var usdToEgpRate) || usdToEgpRate <= 0)
        {
            return Result.Failure<DotChargeAmount>(CommerceErrors.DotPricingNotConfigured);
        }

        var amountEgp = Math.Round(orderTotalUsd * usdToEgpRate, 2, MidpointRounding.AwayFromZero);
        return Result.Success(new DotChargeAmount(amountEgp, TargetCurrency));
    }
}
