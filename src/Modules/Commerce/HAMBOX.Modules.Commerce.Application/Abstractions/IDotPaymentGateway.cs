using HAMBOX.SharedKernel.Results;

namespace HAMBOX.Modules.Commerce.Application.Abstractions;

/// <summary>
/// Server-to-server client for the DOT Partners OTP Landing Page API and Check Transaction Status
/// API. Commerce never talks to DOT's HTTP endpoints directly — every call goes through this seam,
/// so tests substitute a deterministic fake instead of hitting the real provider.
/// <para>
/// partner_id / service_id are single configured values (see <c>DotSettings</c>) and are not
/// parameters here; op_id varies per attempt by selected wallet (<see cref="DotWalletOperator"/>),
/// so it is a parameter on every call below — mirrors <c>IDotFawryPaymentGateway</c>.
/// </para>
/// </summary>
public interface IDotPaymentGateway
{
    /// <summary>
    /// Calls GET Access Token to obtain the encrypted JWE token used to build the OTP landing page
    /// redirect URL.
    /// </summary>
    Task<Result<DotAccessTokenResult>> GetAccessTokenAsync(
        DotAccessTokenRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Builds the URL to redirect the customer's browser to (DOT's OTP landing page), per the
    /// documented <c>token</c>/<c>rurl</c>/<c>partner_tx_timestamp</c>/<c>amount</c> query
    /// parameters. Pure string composition — no network call.
    /// </summary>
    string BuildOtpLandingPageUrl(string token, DotAccessTokenRequest originalRequest);

    /// <summary>Calls Check Transaction Status using DOT's <c>partner_txid</c> lookup. Authoritative — the only source of truth for whether a charge actually succeeded.</summary>
    Task<Result<DotTransactionStatusResult>> CheckTransactionStatusByPartnerTxIdAsync(
        string partnerTxId, string operatorId, CancellationToken cancellationToken = default);

    /// <summary>Calls Check Transaction Status using DOT's <c>dot_txid</c> lookup.</summary>
    Task<Result<DotTransactionStatusResult>> CheckTransactionStatusByDotTxIdAsync(
        string dotTxId, string operatorId, CancellationToken cancellationToken = default);
}

/// <param name="PartnerTxId">HAMBOX-generated unique transaction id.</param>
/// <param name="OperatorId">The DOT <c>op_id</c> for the wallet the customer selected at checkout (<see cref="DotWalletOperatorExtensions.ToOperatorId"/>).</param>
/// <param name="Amount">The amount to encode into the token / pass to the landing page.</param>
/// <param name="RedirectUrl">HAMBOX's own callback URL (sent as <c>rurl</c>).</param>
/// <param name="PartnerTxTimestampUnix">Unix time the transaction was initiated.</param>
public sealed record DotAccessTokenRequest(
    string PartnerTxId,
    string OperatorId,
    decimal Amount,
    string RedirectUrl,
    long PartnerTxTimestampUnix);

/// <summary>
/// resultCode: 0 success, 1008 missing/invalid params, 1009 internal error — per the GET Access
/// Token API spec.
/// </summary>
public sealed record DotAccessTokenResult(int ResultCode, string ResultDesc, string? Token)
{
    public bool IsSuccess => ResultCode == 0 && !string.IsNullOrWhiteSpace(Token);
}

/// <summary>
/// resultCode: 0 successful transaction, 1015 not found among successful transactions, 1008
/// invalid request, 1009 internal error — per the Check Transaction Status API spec.
/// </summary>
public sealed record DotTransactionStatusResult(
    int ResultCode,
    string ResultDesc,
    DateTimeOffset? TransactionTimestampUtc,
    decimal? Amount,
    string? Currency)
{
    public bool IsSuccessfulTransaction => ResultCode == 0;
}
