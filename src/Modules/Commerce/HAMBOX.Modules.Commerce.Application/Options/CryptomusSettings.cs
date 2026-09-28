namespace HAMBOX.Modules.Commerce.Application.Options;

/// <summary>
/// Non-secret shape (<see cref="BaseUrl"/>, timeout) lives in tracked appsettings. <see cref="ApiKey"/>
/// and <see cref="MerchantId"/> are real Cryptomus merchant-account values and must only ever be
/// supplied via environment variables / untracked <c>appsettings.Production.json</c> — never
/// committed. <see cref="ApiKey"/> is used both to sign outgoing requests (<c>sign</c> header) and,
/// server-side, to re-verify payment status before ever trusting an inbound webhook — see
/// <c>CryptomusPaymentVerificationService</c>.
/// </summary>
public sealed class CryptomusSettings
{
    public const string SectionName = "Cryptomus";

    /// <summary>Cryptomus API base URL, e.g. https://api.cryptomus.com.</summary>
    public string BaseUrl { get; init; } = string.Empty;

    /// <summary>The merchant UUID Cryptomus assigned, sent as the <c>merchant</c> header on every request.</summary>
    public string MerchantId { get; init; } = string.Empty;

    /// <summary>The Payment API key — signs outgoing requests and verifies inbound webhook signatures.</summary>
    public string ApiKey { get; init; } = string.Empty;

    /// <summary>
    /// HAMBOX's own public webhook endpoint (this API's <c>POST payments/cryptomus/webhook</c>
    /// route), sent to Cryptomus as <c>url_callback</c>. Must be a publicly reachable HTTPS URL in
    /// production.
    /// </summary>
    public string PublicWebhookUrl { get; init; } = string.Empty;

    /// <summary>
    /// The HAMBOX frontend page to send the customer's browser to once they finish paying on
    /// Cryptomus's hosted page (e.g. <c>https://hambox.example.com/checkout/cryptomus/result</c>).
    /// Sent as <c>url_success</c>/<c>url_return</c>. Never authoritative on its own — the frontend
    /// polls the backend status endpoint, which only ever trusts a verified Cryptomus response.
    /// </summary>
    public string FrontendResultUrl { get; init; } = string.Empty;

    /// <summary>How long an unpaid invoice stays valid, in seconds. Cryptomus default is 3600 (1 hour).</summary>
    public int InvoiceLifetimeSeconds { get; init; } = 3600;

    public int RequestTimeoutSeconds { get; init; } = 15;
}
