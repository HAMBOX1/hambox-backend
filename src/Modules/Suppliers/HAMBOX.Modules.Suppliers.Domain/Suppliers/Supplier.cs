using HAMBOX.Domain.Entities;

namespace HAMBOX.Modules.Suppliers.Domain.Suppliers;

/// <summary>
/// An external supplier integration profile. The marketplace core only ever depends on
/// <c>ISupplierProvider</c>/<c>ISupplierProviderRegistry</c> (Application layer) — this entity
/// merely stores the configuration (which provider, credentials, capabilities) that gets resolved
/// into a provider instance at runtime via <see cref="ProviderType"/>.
/// </summary>
/// <remarks>
/// This is the canonical supplier registry for any future <em>automated</em> integration (an
/// <c>ISupplierProvider</c> that actually calls an external API): it carries a provider type,
/// credentials, and capability flags that a real integration would need, and none of those exist on
/// <c>HAMBOX.Modules.Catalog.Domain.Inventory.InventorySupplier</c>. That older, unrelated Catalog
/// entity is a plain vendor-contact record used only to tag manually-imported inventory batches/codes
/// with "who we bought this from" — it has no credentials or provider concept and is not touched by
/// this module. The two are intentionally not merged; connecting them later (if a manually-tracked
/// vendor is ever promoted to an automated <see cref="Supplier"/>) would need an explicit mapping
/// field, not a shared identity, since their lifecycles and audiences (catalog/inventory admins vs.
/// integration admins) differ.
/// </remarks>
public sealed class Supplier : AggregateRoot, IAuditable, ISoftDeletable
{
    private Supplier()
    {
    }

    private Supplier(
        Guid id,
        string name,
        string code,
        string providerType,
        SupplierAuthenticationType authenticationType,
        string? baseUrl,
        int priority)
        : base(id)
    {
        Name = name;
        Code = code;
        ProviderType = providerType;
        AuthenticationType = authenticationType;
        BaseUrl = baseUrl;
        Priority = priority;
        Status = SupplierStatus.Active;
        IsEnabled = true;
    }

    public string Name { get; private set; } = string.Empty;

    /// <summary>Short unique identifier for the supplier (e.g. "BAMBOO-EU"), distinct from <see cref="ProviderType"/>.</summary>
    public string Code { get; private set; } = string.Empty;

    /// <summary>
    /// Key of the registered <c>ISupplierProvider</c> that implements this supplier's integration
    /// (e.g. "Manual", or a future "Bamboo"). Resolved by <c>ISupplierProviderRegistry</c> — adding
    /// a new value here never requires a change to this entity or any marketplace code.
    /// </summary>
    public string ProviderType { get; private set; } = string.Empty;

    public SupplierStatus Status { get; private set; }
    public int Priority { get; private set; }
    public string? BaseUrl { get; private set; }
    public SupplierAuthenticationType AuthenticationType { get; private set; }

    /// <summary>Provider-specific configuration blob (mirrors the Platform Settings JSON-per-category pattern).</summary>
    public string? SettingsJson { get; private set; }

    public string? ApiKey { get; private set; }
    public string? ApiSecret { get; private set; }
    public string? Username { get; private set; }
    public string? Password { get; private set; }
    public string? BearerToken { get; private set; }

    /// <summary>Reserved for a future OAuth2 flow (client id/secret/token endpoint); stored, not yet acted on.</summary>
    public string? OAuthSettingsJson { get; private set; }

    public bool SupportsInventorySync { get; private set; }
    public bool SupportsPriceSync { get; private set; }
    public bool SupportsReservations { get; private set; }
    public bool SupportsOrderStatus { get; private set; }
    public bool SupportsWebhooks { get; private set; }

    public bool IsEnabled { get; private set; }

    /// <summary>
    /// Generic, provider-agnostic "does this supplier have the credential fields its own
    /// <see cref="AuthenticationType"/> requires" check — never inspects what's actually inside a
    /// field, only whether it's present, so this never leaks or depends on any provider-specific
    /// secret shape. Used by fulfillment routing's READY check; deliberately conservative (a
    /// misconfigured supplier fails closed here rather than being discovered mid-purchase).
    /// </summary>
    public bool HasCredentialsConfigured => AuthenticationType switch
    {
        SupplierAuthenticationType.None => true,
        SupplierAuthenticationType.ApiKey or SupplierAuthenticationType.BasicAuth =>
            !string.IsNullOrWhiteSpace(ApiKey) && !string.IsNullOrWhiteSpace(ApiSecret),
        SupplierAuthenticationType.BearerToken => !string.IsNullOrWhiteSpace(BearerToken),
        SupplierAuthenticationType.OAuth2 => !string.IsNullOrWhiteSpace(OAuthSettingsJson),
        _ => false,
    };

    public bool IsDeleted { get; private set; }
    public DateTimeOffset? DeletedOnUtc { get; private set; }
    public string? CreatedBy { get; set; }
    public string? ModifiedBy { get; set; }

    public static Supplier Create(
        string name,
        string code,
        string providerType,
        SupplierAuthenticationType authenticationType,
        string? baseUrl,
        int priority)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerType);

        return new Supplier(
            Guid.NewGuid(),
            name.Trim(),
            code.Trim().ToUpperInvariant(),
            providerType.Trim(),
            authenticationType,
            string.IsNullOrWhiteSpace(baseUrl) ? null : baseUrl.Trim(),
            priority);
    }

    public void UpdateDetails(
        string name,
        string providerType,
        SupplierAuthenticationType authenticationType,
        string? baseUrl,
        bool supportsInventorySync,
        bool supportsPriceSync,
        bool supportsReservations,
        bool supportsOrderStatus,
        bool supportsWebhooks)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerType);

        Name = name.Trim();
        ProviderType = providerType.Trim();
        AuthenticationType = authenticationType;
        BaseUrl = string.IsNullOrWhiteSpace(baseUrl) ? null : baseUrl.Trim();
        SupportsInventorySync = supportsInventorySync;
        SupportsPriceSync = supportsPriceSync;
        SupportsReservations = supportsReservations;
        SupportsOrderStatus = supportsOrderStatus;
        SupportsWebhooks = supportsWebhooks;
    }

    public void UpdateSettings(string? settingsJson) => SettingsJson = settingsJson;

    public void UpdateCredentials(
        string? apiKey,
        string? apiSecret,
        string? username,
        string? password,
        string? bearerToken,
        string? oAuthSettingsJson)
    {
        ApiKey = NullIfWhitespace(apiKey);
        ApiSecret = NullIfWhitespace(apiSecret);
        Username = NullIfWhitespace(username);
        Password = NullIfWhitespace(password);
        BearerToken = NullIfWhitespace(bearerToken);
        OAuthSettingsJson = NullIfWhitespace(oAuthSettingsJson);
    }

    public void UpdatePriority(int priority) => Priority = priority;

    public void Enable()
    {
        IsEnabled = true;
        if (Status == SupplierStatus.Inactive)
        {
            Status = SupplierStatus.Active;
        }
    }

    public void Disable()
    {
        IsEnabled = false;
        Status = SupplierStatus.Inactive;
    }

    public void Delete()
    {
        IsDeleted = true;
        DeletedOnUtc = DateTimeOffset.UtcNow;
        IsEnabled = false;
    }

    private static string? NullIfWhitespace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
