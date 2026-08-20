using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Errors;
using HAMBOX.SharedKernel.Results;

namespace HAMBOX.Modules.Commerce.Infrastructure.Services;

/// <summary>
/// Default <see cref="IDotPricePointResolver"/>: always fails cleanly. DOT's GET Access Token call
/// takes no currency parameter, and currency only ever appears in DOT's own responses tied to the
/// configured <c>op_id</c>/<c>service_id</c> — meaning whether this DOT account supports charging
/// an arbitrary order total or only a fixed operator-side price point has not been confirmed with
/// DOT. Rather than guess, DOT checkout is wired end-to-end but reports "not yet available" until
/// the client confirms the pricing model and a real resolver is registered in its place — see
/// <see cref="IDotPricePointResolver"/> for the full rationale.
/// </summary>
internal sealed class NotConfiguredDotPricePointResolver : IDotPricePointResolver
{
    public Result<DotChargeAmount> Resolve(decimal orderTotalUsd, string countryCode) =>
        Result.Failure<DotChargeAmount>(CommerceErrors.DotPricingNotConfigured);
}
