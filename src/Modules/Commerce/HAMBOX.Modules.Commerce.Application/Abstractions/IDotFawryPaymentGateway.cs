using HAMBOX.SharedKernel.Results;

namespace HAMBOX.Modules.Commerce.Application.Abstractions;

/// <summary>
/// Server-to-server client for DOT's Fawry Direct Billing API (Direct Billing, Check Transaction
/// Status), per the vendor-provided "Direct Billing API Integration" PDF and the Fawry-specific
/// field usage confirmed directly by DOT (May Albalakha): <c>extraField1</c> = customer email
/// (mandatory), <c>extraField2</c> = customer name (optional), <c>extraField3</c> = JSON array of
/// order line items. This is a distinct DOT product from the carrier-billing OTP flow
/// (<c>IDotPaymentGateway</c>) — it has its own endpoint, request/response shape, and does not share
/// an implementation with it. Reused for Egyptian mobile wallets beyond Fawry (Orange Cash, Vodafone
/// Cash) — same request/response shape and credentials, only <c>opId</c> differs (see
/// <c>DotFawryWalletOperator</c>).
/// <para>
/// partner_id / service_id are single configured values (see <c>DotFawrySettings</c>) and are not
/// parameters here; op_id varies per attempt by selected wallet, so it is a parameter on every call
/// below.
/// </para>
/// </summary>
public interface IDotFawryPaymentGateway
{
    /// <summary>
    /// Calls the Direct Billing API. A <c>resultCode</c> of <c>"1000"</c> means the transaction is
    /// still being processed — <see cref="DotFawryChargeResult.IsProcessing"/> — and must never be
    /// treated as a successful payment; DOT's own docs confirm some operators resolve
    /// asynchronously via the Transaction Status Notification webhook or a later Check Transaction
    /// Status call.
    /// </summary>
    Task<Result<DotFawryChargeResult>> ChargeAsync(
        DotFawryChargeRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Calls Check Transaction Status using DOT's <c>partnerTransId</c> lookup. Authoritative — the
    /// only source of truth for whether a charge actually succeeded. <paramref name="operatorId"/>
    /// must be the same <c>opId</c> sent in the original Direct Billing request for this attempt
    /// (<c>PaymentAttempt.OperatorId</c>) — the spec requires it to match.
    /// </summary>
    Task<Result<DotFawryTransactionStatusResult>> CheckTransactionStatusByPartnerTxIdAsync(
        string partnerTxId, string operatorId, CancellationToken cancellationToken = default);

    /// <summary>Calls Check Transaction Status using DOT's <c>dotTransId</c> lookup. See <see cref="CheckTransactionStatusByPartnerTxIdAsync"/> for <paramref name="operatorId"/>.</summary>
    Task<Result<DotFawryTransactionStatusResult>> CheckTransactionStatusByDotTxIdAsync(
        string dotTxId, string operatorId, CancellationToken cancellationToken = default);
}

/// <param name="PartnerTxId">HAMBOX-generated unique transaction id.</param>
/// <param name="OperatorId">
/// The DOT <c>opId</c> for the wallet the customer selected at checkout
/// (<see cref="DotFawryWalletOperator.ToOperatorId"/>) — the only field that varies between Fawry,
/// Orange Cash, and Vodafone Cash; everything else in this request/the auth headers is identical.
/// </param>
/// <param name="Msisdn">The customer's mobile number — Fawry SMS's the reference number to this number.</param>
/// <param name="Amount">The amount to charge, from <c>IDotFawryChargeAmountResolver</c>.</param>
/// <param name="CustomerEmail">Sent as <c>extraField1</c> — mandatory for this DOT service/operator.</param>
/// <param name="CustomerName">Sent as <c>extraField2</c> — optional for this DOT service/operator.</param>
/// <param name="Items">Sent as the <c>extraField3</c> JSON array — built from the authoritative Order/OrderItems, never client-supplied prices.</param>
public sealed record DotFawryChargeRequest(
    string PartnerTxId,
    string OperatorId,
    string Msisdn,
    decimal Amount,
    string CustomerEmail,
    string? CustomerName,
    IReadOnlyList<DotFawryOrderItem> Items);

/// <summary>One line item as sent in the Direct Billing request's <c>extraField3</c> JSON array.</summary>
public sealed record DotFawryOrderItem(string ItemId, string Description, decimal Price, int Quantity);

/// <summary>
/// resultCode is a documented string enum for this API (unlike the OTP product's integer
/// resultCode): "0" success, "1000" processing (never final), "1004" insufficient balance, "1005"
/// other billing error, "1007" invalid price point, "1008" invalid request, "1009"/"1099" internal
/// error, "1010" blacklisted, "1011" invalid MSISDN, "1013" not subscribed, "1014" attempts
/// exceeded — per the Direct Billing API spec.
/// </summary>
public sealed record DotFawryChargeResult(
    string ResultCode,
    string ResultDesc,
    string? DotTransId,
    /// <summary>
    /// The provider's response <c>extraField1</c> for this Fawry service — confirmed by DOT to
    /// carry the Fawry reference number the customer enters into their Fawry wallet. Not part of
    /// the generic Direct Billing response schema (which only documents resultCode/resultDesc/
    /// dotTransId); this is a Fawry-operator-specific behavior confirmed directly by DOT, not
    /// guessed from the generic spec.
    /// </summary>
    string? FawryReferenceNumber)
{
    public bool IsProcessing => ResultCode == "1000";

    public bool IsImmediateSuccess => ResultCode == "0";
}

/// <summary>
/// resultCode is this Check Transaction Status call's own outcome ("0" found, "1006" not found,
/// "1008" invalid request, "1005"/"1009"/"1099" errors). BillingTransactionResultCode is the
/// original billing transaction's actual result and is only populated when ResultCode is "0" — see
/// the Direct Billing API's Check Transaction Status section. Unlike the OTP product's Check
/// Transaction Status response, DOT's Direct Billing product does not return amount/currency here —
/// there is nothing to cross-verify the charged amount against for this product.
/// </summary>
public sealed record DotFawryTransactionStatusResult(
    string ResultCode,
    string ResultDesc,
    string? BillingTransactionResultCode,
    string? BillingTransactionResultDesc,
    string? DotTransId)
{
    public bool IsSuccessfulTransaction => ResultCode == "0" && BillingTransactionResultCode == "0";

    public bool IsStillProcessing => ResultCode == "0" && BillingTransactionResultCode == "1000";
}
