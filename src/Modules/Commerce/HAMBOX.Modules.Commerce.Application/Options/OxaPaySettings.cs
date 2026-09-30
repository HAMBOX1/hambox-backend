namespace HAMBOX.Modules.Commerce.Application.Options;

/// <summary>
/// Non-secret shape (<see cref="BaseUrl"/>, timeout) lives in tracked appsettings.
/// <see cref="MerchantApiKey"/> is a real OxaPay merchant-account value and is only ever supplied
/// through the admin Payment Gateways dashboard (<c>PaymentGatewayConfiguration.ApiKey</c>, encrypted
/// at rest) — unlike Cryptomus, no appsettings/environment-variable fallback is wired for it, since
/// it was never distributed any other way. It signs every outgoing request (<c>merchant_api_key</c>
/// header) and, server-side, verifies inbound webhook signatures — see
/// <c>OxaPayWebhookSignatureVerifier</c>.
/// </summary>
public sealed class OxaPaySettings
{
    public const string SectionName = "OxaPay";

    /// <summary>OxaPay API base URL, e.g. https://api.oxapay.com.</summary>
    public string BaseUrl { get; init; } = string.Empty;

    /// <summary>The OxaPay Merchant API key — sent as the <c>merchant_api_key</c> header on every request and used as the HMAC secret for webhook verification.</summary>
    public string MerchantApiKey { get; init; } = string.Empty;

    /// <summary>
    /// HAMBOX's own public webhook endpoint (this API's <c>POST payments/oxapay/webhook</c> route),
    /// sent to OxaPay as <c>callback_url</c>. Must be a publicly reachable HTTPS URL in production.
    /// </summary>
    public string PublicWebhookUrl { get; init; } = string.Empty;

    /// <summary>
    /// The HAMBOX frontend page to send the customer's browser to once they finish paying on
    /// OxaPay's hosted invoice page. Sent as <c>return_url</c>. Never authoritative on its own —
    /// the frontend polls the backend status endpoint, which only ever trusts a verified OxaPay
    /// response.
    /// </summary>
    public string FrontendResultUrl { get; init; } = string.Empty;

    /// <summary>How long an unpaid invoice stays valid, in minutes. OxaPay accepts 15-2880, default 60.</summary>
    public int InvoiceLifetimeMinutes { get; init; } = 60;

    public int RequestTimeoutSeconds { get; init; } = 15;
}
