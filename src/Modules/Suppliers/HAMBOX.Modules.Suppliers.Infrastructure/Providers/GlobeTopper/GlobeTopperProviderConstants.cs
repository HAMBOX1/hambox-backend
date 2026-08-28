namespace HAMBOX.Modules.Suppliers.Infrastructure.Providers.GlobeTopper;

/// <summary>
/// Fixed, non-configurable facts about the GlobeTopper Partner API (v1.0.11), read from the live OpenAPI
/// document at <c>https://partner.globetopper.com/api/v2/docs/browse</c> (ReDoc UI; raw spec at
/// <c>.../api/v2/docs/schema</c>) and confirmed against real sandbox calls — see
/// docs/integrations/suppliers/README.md for the verification result.
/// </summary>
/// <remarks>
/// <see cref="BaseUrl"/> is deliberately NOT read from <c>Supplier.BaseUrl</c> (admin-editable) — same
/// SSRF rationale as <c>BambooProviderConstants</c>/<c>VisoriaProviderConstants</c>: an admin could
/// otherwise repoint a "GlobeTopper" supplier's traffic at an arbitrary internal host using real
/// GlobeTopper credentials.
///
/// <b>Unlike Bamboo/Visoria, GlobeTopper's own OpenAPI document declares only ONE server</b> —
/// <c>https://partner.sandbox.globetopper.com/api/v2</c> — with no separate production host documented
/// anywhere reachable in this environment. This integration is hardcoded to that one documented host.
/// Do not assume the public portal hostname (<c>partner.globetopper.com</c>, no "sandbox.") is also the
/// API host for production traffic — that was never confirmed. Before routing real, credit-consuming
/// purchases through this integration, confirm with GlobeTopper support/account management whether
/// production uses this same host or a distinct one, and update <see cref="BaseUrl"/> accordingly.
///
/// <see cref="BaseUrl"/> deliberately carries NO path component (host only) — same shape as
/// <c>BambooProviderConstants.BaseUrl</c>/<c>VisoriaProviderConstants.BaseUrl</c>. Every path constant
/// below embeds the <c>/api/v2</c> prefix itself instead. This is not cosmetic: <see cref="HttpClient"/>
/// resolves a request URI that starts with <c>/</c> as absolute-from-host against
/// <c>HttpClient.BaseAddress</c>, which silently DISCARDS the base address's own path component — a
/// <c>BaseUrl</c> of <c>".../api/v2"</c> combined with a request path of <c>"/user"</c> resolves to
/// <c>https://partner.sandbox.globetopper.com/user</c> (a real 404), not <c>.../api/v2/user</c>. Confirmed
/// by a real 404 during initial verification — fixed by moving the <c>/api/v2</c> segment into the paths.
/// </remarks>
internal static class GlobeTopperProviderConstants
{
    public const string ProviderType = "GlobeTopper";

    /// <summary>The only host GlobeTopper's own OpenAPI document declares — see remarks above. No path component — see remarks for why that matters.</summary>
    public const string BaseUrl = "https://partner.sandbox.globetopper.com";

    public const string UserPath = "/api/v2/user";
    public const string CountriesPath = "/api/v2/country/search-countries";
    public const string ProductsPath = "/api/v2/product/search-all-products";
    public const string TransactionByIdPathFormat = "/api/v2/transaction/search-transactions/{0}";

    /// <summary>
    /// <c>{0}</c> = the operator id from <c>/api/v2/product/search-all-products</c> (<c>operator -&gt; id</c>,
    /// NOT <c>BillerID</c>); <c>{1}</c> = the face-value amount, formatted invariant-culture.
    /// </summary>
    public const string PurchasePathFormat = "/api/v2/transaction/do-by-product/{0}/{1}";
}
