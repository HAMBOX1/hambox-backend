namespace HAMBOX.Modules.Suppliers.Infrastructure.Providers.Eneba;

/// <summary>
/// Fixed, non-configurable facts about the Eneba API (https://api.eneba.com/documentation/), read from
/// the live documentation site — no official OpenAPI/Postman collection was available in this
/// environment. See docs/integrations/suppliers/README.md for the full source list and what remains
/// unverified against a real sandbox account.
/// </summary>
/// <remarks>
/// <see cref="BaseUrl"/>/<see cref="OAuthTokenUrl"/> are deliberately NOT read from
/// <c>Supplier.BaseUrl</c> (admin-editable) — same SSRF rationale as <c>BambooProviderConstants</c>/
/// <c>VisoriaProviderConstants</c>/<c>GlobeTopperProviderConstants</c>.
///
/// <b>Only the Sandbox host is documented anywhere reachable in this environment.</b> The getting-started
/// guide states production "uses production equivalent" without giving a URL, and separately that the
/// production GraphQL base URL is "provided in the credentials dashboard" (<c>https://my.eneba.com/api/credentials</c>)
/// — i.e. it may not even be the same for every merchant. This integration is therefore hardcoded to the
/// one documented Sandbox host, exactly like <c>GlobeTopperProviderConstants</c>'s identical single-host
/// situation. <b>Before routing real, credit-consuming purchases through this integration</b>, retrieve
/// the actual production GraphQL URL from the Eneba credentials dashboard and update <see cref="BaseUrl"/>/
/// <see cref="OAuthTokenUrl"/> accordingly (they may need to become per-Supplier-row constants, or gain a
/// documented environment switch, if production truly is merchant-specific).
/// </remarks>
internal static class EnebaProviderConstants
{
    public const string ProviderType = "Eneba";

    /// <summary>The only host Eneba's own documentation gives a concrete URL for — see remarks above.</summary>
    public const string BaseUrl = "https://api-sandbox.eneba.com";

    public const string GraphQlPath = "/graphql/";

    public const string OAuthTokenUrl = "https://api-sandbox.eneba.com/oauth/token";

    /// <summary>
    /// Documented as a fixed, public value every API consumer sends (not a per-merchant secret) —
    /// <c>"client_id": "917611c2-70a5-11e9-00c4-ee691bb8bfaa"</c> in the token-request example. Safe to
    /// keep as a compile-time constant alongside the Auth ID/Auth Secret, which ARE the actual per-merchant secrets.
    /// </summary>
    public const string OAuthClientId = "917611c2-70a5-11e9-00c4-ee691bb8bfaa";

    public const string OAuthGrantType = "api_consumer";

    /// <summary>
    /// Maximum wholesale auction items per <c>S_purchaseWholesaleAuctions</c> call (documented) — not
    /// used by this integration today since <see cref="Application.Abstractions.SupplierPurchaseRequest"/>
    /// only ever carries one <c>ExternalProductId</c> per call, but kept here as the documented fact for
    /// any future multi-item batching.
    /// </summary>
    public const int MaxAuctionItemsPerPurchase = 10;

    /// <summary>Documented per-item quantity bound — see <see cref="EnebaSupplierProvider.MaxQuantityPerPurchase"/>.</summary>
    public const int MinQuantityPerAuctionItem = 1;

    public const int MaxQuantityPerAuctionItem = 2000;
}
