using HAMBOX.SharedKernel.Results;

namespace HAMBOX.Modules.Commerce.Application.Abstractions;

/// <summary>
/// Resolves what to actually charge an order through DOT: the amount and currency to send to
/// DOT's GET Access Token call and to validate DOT's Check Transaction Status response against.
/// <para>
/// This boundary exists because DOT's GET Access Token call takes no currency parameter — currency
/// only appears in DOT's own responses, tied to the configured <c>op_id</c>/<c>service_id</c>. That
/// strongly implies each <c>service_id</c> is a fixed operator-side price point (standard for
/// carrier billing), not a free-form charge of an arbitrary HAMBOX cart total in USD. Whether DOT
/// actually supports arbitrary amounts or only fixed price points for this account has not been
/// confirmed by the client with DOT yet, so no resolution logic is implemented here — see
/// <c>NotConfiguredDotPricePointResolver</c>. Implement the real mapping only once that's confirmed,
/// by registering a different <see cref="IDotPricePointResolver"/> — nothing else in the payment
/// pipeline needs to change.
/// </para>
/// </summary>
public interface IDotPricePointResolver
{
    /// <param name="orderTotalUsd">The order's authoritative total, computed server-side by the same pricing/promotion path every other payment method uses. Never a client-supplied amount.</param>
    /// <param name="countryCode">The customer's checkout country, in case the eventual mapping needs it.</param>
    Result<DotChargeAmount> Resolve(decimal orderTotalUsd, string countryCode);
}

public sealed record DotChargeAmount(decimal Amount, string Currency);
