namespace HAMBOX.Modules.Suppliers.Infrastructure.Providers.CodesWholesale;

/// <summary>
/// Fixed, non-configurable facts about the CodesWholesale API, confirmed against CodesWholesale's own
/// official open-source client libraries (<c>codeswholesale/codeswholesale-sdk-php</c>, API v2 —
/// endpoint paths, request/response field names, and both endpoint constants are read directly from that
/// SDK's source, not the marketing documentation site, which returned HTTP 403 to every fetch attempt in
/// this environment). See docs/integrations/suppliers/README.md for the full source list and what
/// remains unverified against a real sandbox account (notably: the exact OAuth token response shape and
/// the full order-status string vocabulary — neither is exposed by the PHP SDK, which delegates token
/// handling to a generic OAuth2 Guzzle middleware and never branches on order status itself).
/// </summary>
/// <remarks>
/// Unlike Bamboo/GlobeTopper (one shared host for both environments), CodesWholesale documents two
/// genuinely different hosts for Sandbox vs. Production. Both are still compile-time constants — never
/// read from the admin-editable <c>Supplier.BaseUrl</c> — for the same SSRF rationale as every other
/// provider here; which one is used per <c>Supplier</c> row is chosen by the non-secret
/// <c>Supplier.SettingsJson</c> <c>"environment"</c> field (<see cref="CodesWholesaleSupplierSettings"/>),
/// defaulting to Sandbox so a new/misconfigured supplier can never accidentally route to Production.
/// </remarks>
internal static class CodesWholesaleProviderConstants
{
    public const string ProviderType = "CodesWholesale";

    public const string SandboxBaseUrl = "https://sandbox.codeswholesale.com";

    public const string ProductionBaseUrl = "https://api.codeswholesale.com";

    /// <summary>OAuth2 client-credentials token endpoint — same path on both hosts.</summary>
    public const string OAuthTokenPath = "/oauth/token";

    /// <summary>Documented as a fixed scope every API consumer sends (<c>CodesWholesaleClientConfig</c>), not a per-merchant secret.</summary>
    public const string OAuthScope = "administration";

    public const string AccountPath = "/v2/accounts/current";

    public const string ProductsPath = "/v2/products";

    public const string ProductByIdPathFormat = "/v2/products/{0}";

    public const string OrdersPath = "/v2/orders";

    public const string OrderByIdPathFormat = "/v2/orders/{0}";

    /// <summary><c>Order::getHistory</c>'s only documented filters — used by this integration purely as a reconciliation fallback (see <c>CodesWholesaleSupplierProvider.GetOrderStatusAsync</c>'s remarks), never for admin browsing.</summary>
    public const string OrderHistoryPathFormat = "/v2/orders?startFrom={0}&endOn={1}";

    public const string CodeByIdPathFormat = "/v2/codes/{0}";

    /// <summary>Code::STATUS value meaning the delivered text code is present and usable now.</summary>
    public const string CodeStatusText = "Text code";

    /// <summary>Code::STATUS value meaning an image-encoded code is present — this integration cannot deliver a binary image through HAMBOX's text-only <c>OrderLicenseKey</c> pipeline; see <c>ExtractDeliveredCode</c>'s remarks.</summary>
    public const string CodeStatusImage = "Image code";

    /// <summary>Code::STATUS value meaning the code has not been assigned yet — CodesWholesale's documented pre-order mechanism (assigned within up to 14 days, per the public FAQ).</summary>
    public const string CodeStatusPreOrder = "Pre-ordered code";

    /// <summary>Confirmed real, documented business error codes (examples/create-order.php) — every other code stays <see cref="Domain.Fulfillments.SupplierFulfillmentFailureCategory.UnknownProviderState"/>, never guessed.</summary>
    public const int ErrorCodeInsufficientBalance = 10002;

    public const int ErrorCodeProductNotFound = 20001;
}

/// <summary>Non-secret, per-<c>Supplier</c>-row configuration stored in <c>Supplier.SettingsJson</c> — mirrors <c>BambooSupplierSettings</c>'s identical role.</summary>
internal sealed record CodesWholesaleSupplierSettings(string? Environment, bool? AllowPreOrder);
