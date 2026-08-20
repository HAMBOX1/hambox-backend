using HAMBOX.SharedKernel.Results;

namespace HAMBOX.Modules.Commerce.Application.Abstractions;

/// <summary>
/// Resolves what to actually send as <c>amount</c> to DOT's Fawry Direct Billing API for an order.
/// <para>
/// Confirmed by the client: DOT Fawry charges the EGP equivalent of HAMBOX's canonical USD total.
/// <c>DotFawryChargeAmountResolver</c> converts via the same <c>CurrencyExchangeRateService</c> the
/// storefront's display-only conversion already uses — see that class for the rate source
/// (Platform Settings' <c>Currency</c> category, live or static). This does not change HAMBOX's
/// canonical stored-price currency (still USD, per CLAUDE.md §"Things Never To Change") — only the
/// figure actually sent to DOT for this one payment method is converted.
/// </para>
/// </summary>
public interface IDotFawryChargeAmountResolver
{
    /// <param name="orderTotalUsd">The order's authoritative total, computed server-side by the same pricing/promotion path every other payment method uses. Never a client-supplied amount.</param>
    /// <param name="countryCode">The customer's checkout country, in case the eventual mapping needs it.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<Result<DotFawryChargeAmount>> ResolveAsync(decimal orderTotalUsd, string countryCode, CancellationToken cancellationToken = default);
}

public sealed record DotFawryChargeAmount(decimal Amount, string Currency);
