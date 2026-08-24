using HAMBOX.SharedKernel.Results;

namespace HAMBOX.Modules.Commerce.Application.Abstractions;

/// <summary>
/// Resolves what to actually charge an order through DOT: the amount and currency to send to
/// DOT's GET Access Token call and to validate DOT's Check Transaction Status response against.
/// <para>
/// DOT's Partners OTP Landing Page API documents <c>amount</c> as a request parameter on both
/// <c>GET Access Token</c> and the landing page redirect itself, confirming this account supports
/// charging an arbitrary order total rather than only a fixed operator-side price point. Currency
/// is not a request parameter — it's implied by the selected <c>op_id</c> (each wallet operator is
/// tied to one currency), mirroring how <c>IDotFawryChargeAmountResolver</c> already resolves EGP
/// for the sibling Direct Billing product.
/// </para>
/// </summary>
public interface IDotPricePointResolver
{
    /// <param name="orderTotalUsd">The order's authoritative total, computed server-side by the same pricing/promotion path every other payment method uses. Never a client-supplied amount.</param>
    /// <param name="countryCode">The customer's checkout country, in case the eventual mapping needs it.</param>
    Task<Result<DotChargeAmount>> ResolveAsync(
        decimal orderTotalUsd, string countryCode, CancellationToken cancellationToken = default);
}

public sealed record DotChargeAmount(decimal Amount, string Currency);
