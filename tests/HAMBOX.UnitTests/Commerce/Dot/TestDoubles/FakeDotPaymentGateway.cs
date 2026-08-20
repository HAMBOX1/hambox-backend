using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Errors;
using HAMBOX.SharedKernel.Results;

namespace HAMBOX.UnitTests.Commerce.Dot.TestDoubles;

/// <summary>
/// Deterministic stand-in for DOT's real HTTP API. Tests configure <see cref="AccessTokenResult"/>/
/// <see cref="StatusResult"/> up front (or a failure via <see cref="FailAccessToken"/>/
/// <see cref="FailStatusCheck"/>) and assert against <see cref="AccessTokenCalls"/>/<see cref="StatusCheckCalls"/>
/// to verify call counts for idempotency scenarios.
/// </summary>
internal sealed class FakeDotPaymentGateway : IDotPaymentGateway
{
    public List<DotAccessTokenRequest> AccessTokenCalls { get; } = [];

    public List<string> StatusCheckCalls { get; } = [];

    public DotAccessTokenResult AccessTokenResult { get; set; } = new(0, "Token generated successfully", "fake-token");

    public bool FailAccessToken { get; set; }

    public DotTransactionStatusResult StatusResult { get; set; } =
        new(0, "successful transaction", DateTimeOffset.UtcNow, 0m, "USD");

    public bool FailStatusCheck { get; set; }

    public Task<Result<DotAccessTokenResult>> GetAccessTokenAsync(
        DotAccessTokenRequest request, CancellationToken cancellationToken = default)
    {
        AccessTokenCalls.Add(request);
        return Task.FromResult(FailAccessToken
            ? Result.Failure<DotAccessTokenResult>(CommerceErrors.DotProviderUnavailable)
            : Result.Success(AccessTokenResult));
    }

    public string BuildOtpLandingPageUrl(string token, DotAccessTokenRequest originalRequest) =>
        $"https://dot-jo.biz/otp-lp?token={token}&rurl={Uri.EscapeDataString(originalRequest.RedirectUrl)}" +
        $"&partner_tx_timestamp={originalRequest.PartnerTxTimestampUnix}&amount={originalRequest.Amount}";

    public Task<Result<DotTransactionStatusResult>> CheckTransactionStatusByPartnerTxIdAsync(
        string partnerTxId, CancellationToken cancellationToken = default)
    {
        StatusCheckCalls.Add(partnerTxId);
        return Task.FromResult(FailStatusCheck
            ? Result.Failure<DotTransactionStatusResult>(CommerceErrors.DotProviderUnavailable)
            : Result.Success(StatusResult));
    }

    public Task<Result<DotTransactionStatusResult>> CheckTransactionStatusByDotTxIdAsync(
        string dotTxId, CancellationToken cancellationToken = default)
    {
        StatusCheckCalls.Add(dotTxId);
        return Task.FromResult(FailStatusCheck
            ? Result.Failure<DotTransactionStatusResult>(CommerceErrors.DotProviderUnavailable)
            : Result.Success(StatusResult));
    }
}
