using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Errors;
using HAMBOX.SharedKernel.Results;

namespace HAMBOX.UnitTests.Commerce.DotFawry.TestDoubles;

internal sealed class FakeDotFawryChargeAmountResolver : IDotFawryChargeAmountResolver
{
    /// <summary>When true, passes the USD order total straight through unchanged — mirrors what a real resolver would do once the charge currency is confirmed.</summary>
    public bool PassThroughUsd { get; set; } = true;

    public Task<Result<DotFawryChargeAmount>> ResolveAsync(
        decimal orderTotalUsd, string countryCode, CancellationToken cancellationToken = default) =>
        Task.FromResult(PassThroughUsd
            ? Result.Success(new DotFawryChargeAmount(orderTotalUsd, "USD"))
            : Result.Failure<DotFawryChargeAmount>(CommerceErrors.DotFawryPricingNotConfigured));
}
