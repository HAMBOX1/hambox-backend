using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Errors;
using HAMBOX.SharedKernel.Results;

namespace HAMBOX.UnitTests.Commerce.Dot.TestDoubles;

internal sealed class FakeDotPricePointResolver : IDotPricePointResolver
{
    /// <summary>When set, every <see cref="ResolveAsync"/> call returns this fixed amount/currency regardless of the order total — mirrors a real "fixed price point" implementation.</summary>
    public DotChargeAmount? FixedAmount { get; set; }

    /// <summary>When true (and <see cref="FixedAmount"/> is unset), passes the USD order total straight through unchanged — mirrors what an "arbitrary amount" implementation would do.</summary>
    public bool PassThroughUsd { get; set; } = true;

    public Task<Result<DotChargeAmount>> ResolveAsync(
        decimal orderTotalUsd, string countryCode, CancellationToken cancellationToken = default)
    {
        if (FixedAmount is not null)
        {
            return Task.FromResult(Result.Success(FixedAmount));
        }

        return Task.FromResult(PassThroughUsd
            ? Result.Success(new DotChargeAmount(orderTotalUsd, "USD"))
            : Result.Failure<DotChargeAmount>(CommerceErrors.DotPricingNotConfigured));
    }
}
