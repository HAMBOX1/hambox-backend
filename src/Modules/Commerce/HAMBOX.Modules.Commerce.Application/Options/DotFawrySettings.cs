namespace HAMBOX.Modules.Commerce.Application.Options;

/// <summary>
/// Configuration for DOT's Fawry Direct Billing product (server-to-server cash payment via Fawry
/// reference number) — a distinct DOT integration from the carrier-billing OTP flow configured
/// under <see cref="DotSettings"/>. Non-secret shape (BaseUrl, paths, timeout) lives in tracked
/// appsettings; <see cref="Username"/>, <see cref="Password"/>, <see cref="PartnerId"/>,
/// <see cref="ServiceId"/> are real DOT-partner-account values and must only ever be supplied via
/// environment variables / untracked <c>appsettings.Production.json</c> — never committed. See
/// <c>DotFawrySettingsValidator</c> for startup fail-fast format checks.
/// </summary>
public sealed class DotFawrySettings
{
    public const string SectionName = "DotFawry";

    /// <summary>DOT API host, e.g. <c>https://dot-jo.biz</c> (production) or <c>http://dot-jo.biz:8</c> (staging).</summary>
    public string BaseUrl { get; init; } = string.Empty;

    /// <summary>
    /// Path appended to <see cref="BaseUrl"/> for the Direct Billing POST. Differs between
    /// production (<c>/lb2/PartnersDirectBilling/</c>) and staging
    /// (<c>/PartnersDirectBilling-Staging/</c>) per the DOT "Direct Billing API Integration" spec —
    /// override for sandbox testing without a code change.
    /// </summary>
    public string DirectBillingPath { get; init; } = "/lb2/PartnersDirectBilling/";

    /// <summary>
    /// Path prefix appended to <see cref="BaseUrl"/> for Check Transaction Status calls, before the
    /// <c>{opId}/{serviceId}/{transactionId}</c> segments. The spec only documents a production URL
    /// for this call — override only if DOT confirms a staging equivalent.
    /// </summary>
    public string CheckTransactionStatusPath { get; init; } = "/lb2/PartnersDirectBilling/check-transaction-status/";

    /// <summary>The partner (merchant) unique identifier DOT assigned, sent as the <c>PartnerId</c> header.</summary>
    public string PartnerId { get; init; } = string.Empty;

    /// <summary>The DOT-provided service identifier for HAMBOX's Fawry product configuration.</summary>
    public string ServiceId { get; init; } = string.Empty;

    /// <summary>
    /// The DOT operator ID for the Fawry integration (confirmed by DOT as <c>141</c>). Sent as
    /// <c>opId</c> in every Direct Billing / Check Transaction Status call.
    /// </summary>
    public string OperatorId { get; init; } = string.Empty;

    /// <summary>Basic-auth username for the Direct Billing / Check Transaction Status APIs.</summary>
    public string Username { get; init; } = string.Empty;

    /// <summary>Basic-auth password for the Direct Billing / Check Transaction Status APIs.</summary>
    public string Password { get; init; } = string.Empty;

    public int RequestTimeoutSeconds { get; init; } = 15;
}
