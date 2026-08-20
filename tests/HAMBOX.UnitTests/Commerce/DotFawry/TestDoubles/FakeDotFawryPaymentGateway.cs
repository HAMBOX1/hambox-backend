using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Errors;
using HAMBOX.SharedKernel.Results;

namespace HAMBOX.UnitTests.Commerce.DotFawry.TestDoubles;

/// <summary>
/// Deterministic stand-in for DOT's real Fawry Direct Billing HTTP API. Tests configure
/// <see cref="ChargeResult"/>/<see cref="StatusResult"/> up front (or a failure via
/// <see cref="FailCharge"/>/<see cref="FailStatusCheck"/>) and assert against
/// <see cref="ChargeCalls"/>/<see cref="StatusCheckCalls"/> to verify call counts for idempotency
/// scenarios.
/// </summary>
internal sealed class FakeDotFawryPaymentGateway : IDotFawryPaymentGateway
{
    public List<DotFawryChargeRequest> ChargeCalls { get; } = [];

    public List<string> StatusCheckCalls { get; } = [];

    public DotFawryChargeResult ChargeResult { get; set; } =
        new("1000", "the transaction is being processed", "dot-trans-1", "9638322189");

    public bool FailCharge { get; set; }

    public DotFawryTransactionStatusResult StatusResult { get; set; } =
        new("0", "Billing transaction result code has been found", "0", "Successfully charged", "dot-trans-1");

    public bool FailStatusCheck { get; set; }

    public Task<Result<DotFawryChargeResult>> ChargeAsync(
        DotFawryChargeRequest request, CancellationToken cancellationToken = default)
    {
        ChargeCalls.Add(request);
        return Task.FromResult(FailCharge
            ? Result.Failure<DotFawryChargeResult>(CommerceErrors.DotFawryProviderUnavailable)
            : Result.Success(ChargeResult));
    }

    public Task<Result<DotFawryTransactionStatusResult>> CheckTransactionStatusByPartnerTxIdAsync(
        string partnerTxId, CancellationToken cancellationToken = default)
    {
        StatusCheckCalls.Add(partnerTxId);
        return Task.FromResult(FailStatusCheck
            ? Result.Failure<DotFawryTransactionStatusResult>(CommerceErrors.DotFawryProviderUnavailable)
            : Result.Success(StatusResult));
    }

    public Task<Result<DotFawryTransactionStatusResult>> CheckTransactionStatusByDotTxIdAsync(
        string dotTxId, CancellationToken cancellationToken = default)
    {
        StatusCheckCalls.Add(dotTxId);
        return Task.FromResult(FailStatusCheck
            ? Result.Failure<DotFawryTransactionStatusResult>(CommerceErrors.DotFawryProviderUnavailable)
            : Result.Success(StatusResult));
    }
}
