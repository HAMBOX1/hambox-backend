using HAMBOX.SharedKernel.Results;

namespace HAMBOX.Modules.Commerce.Application.Abstractions;

/// <summary>
/// Server-to-server client for the OxaPay Merchant API (Generate Invoice, Payment Information).
/// Commerce never talks to OxaPay's HTTP endpoints directly — every call goes through this seam,
/// mirroring <c>ICryptomusPaymentGateway</c>. A second, independent crypto gateway alongside
/// Cryptomus — not a replacement.
/// </summary>
public interface IOxaPayPaymentGateway
{
    /// <summary>Calls Generate Invoice (<c>POST /v1/payment/invoice</c>) to open a new hosted payment page for the given order.</summary>
    Task<Result<OxaPayInvoiceResult>> CreateInvoiceAsync(
        OxaPayCreateInvoiceRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Calls Payment Information (<c>GET /v1/payment/{track_id}</c>) by OxaPay's own <c>track_id</c>
    /// (returned from <see cref="CreateInvoiceAsync"/> and stored as HAMBOX's <c>ProviderTransactionId</c>
    /// — OxaPay has no separate merchant-supplied lookup key the way Cryptomus's <c>order_id</c> is).
    /// Authoritative — the only source of truth for whether a charge actually completed; an inbound
    /// webhook is never trusted by itself, exactly like Cryptomus's Payment Information re-verification.
    /// </summary>
    Task<Result<OxaPayInvoiceResult>> GetPaymentInfoByTrackIdAsync(
        string trackId, CancellationToken cancellationToken = default);
}

/// <param name="OrderId">HAMBOX-generated unique transaction id, sent as OxaPay's <c>order_id</c>.</param>
/// <param name="AmountUsd">The USD amount to invoice (OxaPay's <c>amount</c> field defaults to USD when no <c>currency</c> is specified).</param>
/// <param name="ReturnUrl">HAMBOX's own result page, with <c>?paymentAttemptId=</c> already appended. Sent as <c>return_url</c>.</param>
public sealed record OxaPayCreateInvoiceRequest(string OrderId, decimal AmountUsd, string ReturnUrl);

/// <summary>
/// An OxaPay invoice/payment record, whichever endpoint returned it. <see cref="Status"/> is one of
/// OxaPay's documented invoice statuses ("new", "waiting", "paying", "paid", "manual_accept",
/// "underpaid", "refunding", "refunded", "expired").
/// </summary>
public sealed record OxaPayInvoiceResult(
    string TrackId,
    string? OrderId,
    string? PaymentUrl,
    string Status,
    decimal? Amount,
    string? Currency)
{
    public bool IsSuccessfulPayment => Status is "paid" or "manual_accept";

    /// <summary>OxaPay hasn't reached a terminal answer yet — not a failure, just "ask again shortly". "underpaid" is included since the payer may still top up before the invoice's lifetime elapses.</summary>
    public bool IsStillProcessing => Status is "new" or "waiting" or "paying" or "underpaid";
}
