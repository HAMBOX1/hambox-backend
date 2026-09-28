using HAMBOX.SharedKernel.Results;

namespace HAMBOX.Modules.Commerce.Application.Abstractions;

/// <summary>
/// Server-to-server client for the Cryptomus Merchant API (Create Invoice, Payment Information).
/// Commerce never talks to Cryptomus's HTTP endpoints directly — every call goes through this seam,
/// mirroring <c>IDotPaymentGateway</c>.
/// </summary>
public interface ICryptomusPaymentGateway
{
    /// <summary>Calls Create Invoice (<c>POST /v1/payment</c>) to open a new hosted payment page for the given order.</summary>
    Task<Result<CryptomusInvoiceResult>> CreateInvoiceAsync(
        CryptomusCreateInvoiceRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Calls Payment Information (<c>POST /v1/payment/info</c>) by our own <c>order_id</c>
    /// (HAMBOX's <c>PartnerTxId</c>). Authoritative — the only source of truth for whether a charge
    /// actually completed; an inbound webhook is never trusted by itself, exactly like DOT's
    /// Check-Transaction-Status re-verification.
    /// </summary>
    Task<Result<CryptomusInvoiceResult>> GetPaymentInfoByOrderIdAsync(
        string orderId, CancellationToken cancellationToken = default);
}

/// <param name="OrderId">HAMBOX-generated unique transaction id, sent as Cryptomus's <c>order_id</c>.</param>
/// <param name="AmountUsd">The USD amount to invoice.</param>
/// <param name="ReturnUrl">
/// HAMBOX's own result page, with <c>?paymentAttemptId=</c> already appended (built before this
/// call, since the attempt's id is known before Cryptomus is ever contacted — mirrors how
/// <c>rurl</c> carries no such id for DOT, but here it's simpler to just put it on the URL Cryptomus
/// redirects back to, since Cryptomus's own success redirect carries no verifiable payment data
/// worth parsing instead). Sent as both <c>url_return</c> and <c>url_success</c>.
/// </param>
public sealed record CryptomusCreateInvoiceRequest(string OrderId, decimal AmountUsd, string ReturnUrl);

/// <summary>
/// A Cryptomus invoice/payment record, whichever endpoint returned it. <see cref="Status"/> is one
/// of Cryptomus's documented payment statuses ("paid", "paid_over", "process", "check",
/// "confirm_check", "wrong_amount", "wrong_amount_waiting", "fail", "cancel", "system_fail",
/// "refund_process", "refund_fail", "refund_paid", "locked").
/// </summary>
public sealed record CryptomusInvoiceResult(
    string Uuid,
    string OrderId,
    string? Url,
    string Status,
    bool IsFinal,
    decimal? PaymentAmountUsd,
    string? PayerCurrency)
{
    public bool IsSuccessfulPayment => Status is "paid" or "paid_over";

    /// <summary>Cryptomus hasn't reached a terminal answer yet — not a failure, just "ask again shortly".</summary>
    public bool IsStillProcessing => Status is "process" or "check" or "confirm_check" or "wrong_amount_waiting" || !IsFinal && !IsSuccessfulPayment && Status is not ("fail" or "cancel" or "system_fail");
}
