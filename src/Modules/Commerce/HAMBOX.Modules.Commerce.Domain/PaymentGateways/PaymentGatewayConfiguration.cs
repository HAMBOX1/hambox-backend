using HAMBOX.Domain.Entities;

namespace HAMBOX.Modules.Commerce.Domain.PaymentGateways;

/// <summary>
/// Admin-managed configuration for one payment gateway integration (Cryptomus, DOT, DOT Fawry, and
/// any future provider). Replaces per-gateway <c>appsettings</c>/environment-variable configuration
/// with a database row an admin can edit from the dashboard — see
/// <c>IPaymentGatewayConfigurationProvider</c> for how gateway classes read this at runtime.
/// <para>
/// Field names are deliberately generic (<see cref="AccountId"/>/<see cref="SecondaryId"/> rather
/// than e.g. "MerchantId"/"PartnerId") so a brand-new gateway never needs a schema change: it maps
/// its own credential shape onto these same four slots (two plaintext identifiers, two encrypted
/// secrets) plus <see cref="AdditionalConfigJson"/> for anything else.
/// </para>
/// </summary>
public sealed class PaymentGatewayConfiguration : Entity
{
    private PaymentGatewayConfiguration()
    {
    }

    private PaymentGatewayConfiguration(Guid id, string gatewayKey, string displayName)
        : base(id)
    {
        GatewayKey = gatewayKey;
        DisplayName = displayName;
        IsEnabled = false;
        IsTestMode = false;
    }

    /// <summary>Stable, lowercase key identifying the gateway (e.g. "cryptomus", "dot", "dotfawry"). Never changes after creation.</summary>
    public string GatewayKey { get; private set; } = string.Empty;

    public string DisplayName { get; private set; } = string.Empty;

    public bool IsEnabled { get; private set; }

    public bool IsTestMode { get; private set; }

    /// <summary>This gateway's own fee/tax percentage (0-100) applied instead of the platform default rate when orders are paid through it. Null means "use the platform default".</summary>
    public decimal? FeePercent { get; private set; }

    public string? BaseUrl { get; private set; }

    /// <summary>Plaintext account/merchant/partner identifier — not considered a secret by any provider seen so far.</summary>
    public string? AccountId { get; private set; }

    /// <summary>Plaintext secondary identifier a provider may need alongside <see cref="AccountId"/> (e.g. DOT's ServiceId). Null if unused.</summary>
    public string? SecondaryId { get; private set; }

    /// <summary>Encrypted at rest — see <c>CommerceDbContext.ApplyCredentialEncryption</c>.</summary>
    public string? ApiKey { get; private set; }

    /// <summary>Encrypted at rest, for providers needing a second secret (DOT's password, etc.). Null if unused.</summary>
    public string? ApiSecret { get; private set; }

    /// <summary>HAMBOX's own public webhook/callback URL to give this provider. Not a secret, but gets the provider's route wrong easily — keep it admin-editable and reviewable.</summary>
    public string? WebhookUrl { get; private set; }

    /// <summary>Frontend page the customer's browser returns to after paying/authorizing.</summary>
    public string? FrontendResultUrl { get; private set; }

    /// <summary>Free-form JSON bag for gateway-specific extra fields (timeouts, invoice lifetime, provider-specific paths, ...) that don't warrant their own column.</summary>
    public string? AdditionalConfigJson { get; private set; }

    public DateTimeOffset ModifiedOnUtc { get; private set; }

    public Guid? ModifiedByUserId { get; private set; }

    public static PaymentGatewayConfiguration Create(string gatewayKey, string displayName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gatewayKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

        return new PaymentGatewayConfiguration(Guid.NewGuid(), gatewayKey.Trim().ToLowerInvariant(), displayName.Trim());
    }

    public void UpdateGeneral(
        string displayName,
        bool isTestMode,
        decimal? feePercent,
        string? baseUrl,
        string? accountId,
        string? webhookUrl,
        string? frontendResultUrl,
        string? additionalConfigJson,
        Guid? modifiedByUserId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        if (feePercent is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(feePercent), "Fee percent must be between 0 and 100.");
        }

        DisplayName = displayName.Trim();
        IsTestMode = isTestMode;
        FeePercent = feePercent;
        BaseUrl = NullIfWhitespace(baseUrl);
        AccountId = NullIfWhitespace(accountId);
        WebhookUrl = NullIfWhitespace(webhookUrl);
        FrontendResultUrl = NullIfWhitespace(frontendResultUrl);
        AdditionalConfigJson = NullIfWhitespace(additionalConfigJson);
        Touch(modifiedByUserId);
    }

    /// <summary>
    /// Only overwrites a credential field when a new, non-blank value is supplied — a blank field
    /// means "leave the stored value alone", never "clear it". To actually clear a credential, use a
    /// dedicated clear action instead of this method.
    /// </summary>
    public void UpdateCredentials(string? apiKey, string? apiSecret, string? secondaryId, Guid? modifiedByUserId)
    {
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            ApiKey = apiKey.Trim();
        }

        if (!string.IsNullOrWhiteSpace(apiSecret))
        {
            ApiSecret = apiSecret.Trim();
        }

        if (!string.IsNullOrWhiteSpace(secondaryId))
        {
            SecondaryId = secondaryId.Trim();
        }

        Touch(modifiedByUserId);
    }

    public void Enable(Guid? modifiedByUserId)
    {
        IsEnabled = true;
        Touch(modifiedByUserId);
    }

    public void Disable(Guid? modifiedByUserId)
    {
        IsEnabled = false;
        Touch(modifiedByUserId);
    }

    private void Touch(Guid? modifiedByUserId)
    {
        ModifiedOnUtc = DateTimeOffset.UtcNow;
        ModifiedByUserId = modifiedByUserId;
    }

    private static string? NullIfWhitespace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
