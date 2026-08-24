namespace HAMBOX.Modules.Commerce.Application.Options;

/// <summary>
/// Non-secret shape lives in tracked appsettings (BaseUrl, timeout). <see cref="Username"/>,
/// <see cref="Password"/>, <see cref="PartnerId"/>, <see cref="ServiceId"/> are real
/// DOT-partner-account values and must only ever be supplied via environment variables / untracked
/// <c>appsettings.Production.json</c> / a secret manager — never committed. See
/// <c>DotSettingsValidator</c> for the startup fail-fast checks. There is no <c>OperatorId</c> here
/// — <c>op_id</c> is provider-confirmed, fixed, per-wallet data (Orange Cash 117 / Vodafone Cash
/// 114), not per-environment config — see <see cref="DotWalletOperator"/>.
/// </summary>
public sealed class DotSettings
{
    public const string SectionName = "Dot";

    /// <summary>DOT API base URL, e.g. https://dot-jo.biz.</summary>
    public string BaseUrl { get; init; } = string.Empty;

    /// <summary>The partner (merchant) unique identifier DOT assigned.</summary>
    public string PartnerId { get; init; } = string.Empty;

    /// <summary>The DOT-provided service identifier for the product/price point HAMBOX is configured to sell through DOT.</summary>
    public string ServiceId { get; init; } = string.Empty;

    /// <summary>Basic-auth username for the GET Access Token / Check Transaction Status APIs.</summary>
    public string Username { get; init; } = string.Empty;

    /// <summary>Basic-auth password for the GET Access Token / Check Transaction Status APIs.</summary>
    public string Password { get; init; } = string.Empty;

    /// <summary>
    /// HAMBOX's own public callback endpoint (this API's <c>GET payments/dot/callback</c> route),
    /// sent to DOT as <c>rurl</c>. Must be a publicly reachable HTTPS URL in production.
    /// </summary>
    public string PublicRedirectUrl { get; init; } = string.Empty;

    /// <summary>
    /// The HAMBOX frontend page to send the customer's browser to once the callback endpoint has
    /// processed DOT's redirect (e.g. <c>https://hambox.example.com/checkout/dot/result</c>). Only
    /// <c>?paymentAttemptId=</c> is ever appended — the frontend gets its authoritative status from
    /// the backend status endpoint, never from anything DOT put on the URL.
    /// </summary>
    public string FrontendResultUrl { get; init; } = string.Empty;

    public int RequestTimeoutSeconds { get; init; } = 15;
}
