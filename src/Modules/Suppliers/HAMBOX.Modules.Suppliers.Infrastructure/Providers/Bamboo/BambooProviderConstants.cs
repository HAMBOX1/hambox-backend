namespace HAMBOX.Modules.Suppliers.Infrastructure.Providers.Bamboo;

/// <summary>
/// Fixed, non-configurable facts about the Bamboo Card Portal API (https://docs.bamboocardportal.com/).
/// <see cref="BaseUrl"/> is deliberately NOT read from <c>Supplier.BaseUrl</c> (which is admin-editable)
/// — an admin could otherwise repoint a "Bamboo" supplier's traffic at an arbitrary internal host using
/// real Bamboo credentials, defeating <c>SupplierBaseUrlValidator</c>'s SSRF protection by construction
/// (the validator only checks that a URL isn't private/internal, it can't know a URL "isn't really
/// Bamboo"). Keeping the host a compile-time constant closes that off entirely for this provider.
/// </summary>
internal static class BambooProviderConstants
{
    public const string ProviderType = "Bamboo";

    /// <summary>
    /// Sandbox and production share this exact host — per the documentation ("Sandbox vs Production
    /// Environments"), the two are distinguished only by which Client ID/Secret is sent, not by URL.
    /// </summary>
    public const string BaseUrl = "https://api.bamboocardportal.com";

    // Paths as extracted from the documentation prose per-endpoint-page. The docs show inconsistent
    // version segments across pages (v1.0 vs v1, casing of "Integration") — these were not verified
    // against Bamboo's official Postman collection (not available in this environment). Verify against
    // that collection before any real sandbox call; see docs/integrations/suppliers/README.md.
    public const string AccountsPath = "/api/integration/v1.0/accounts";
    public const string PlaceOrderPath = "/api/integration/v1.0/orders/checkout";
    public const string GetOrderPathFormat = "/api/integration/v1.0/orders/{0}";

    /// <summary>Confirmed against Bamboo's public API reference (docs.bamboocardportal.com/docs/get-catalog) — note the v2.0 segment, unlike the v1.0 endpoints above.</summary>
    public const string CatalogPath = "/api/integration/v2.0/catalog";
}
