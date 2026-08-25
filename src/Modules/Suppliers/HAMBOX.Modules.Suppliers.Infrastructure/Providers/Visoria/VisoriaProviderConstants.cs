namespace HAMBOX.Modules.Suppliers.Infrastructure.Providers.Visoria;

/// <summary>
/// Fixed, non-configurable facts about the Visoria Developer API (https://api.visoria.digital), read
/// from the OpenAPI spec at docs/integrations/suppliers/api-2.json. <see cref="BaseUrl"/> is
/// deliberately NOT read from <c>Supplier.BaseUrl</c> (admin-editable) — same SSRF rationale as
/// <c>BambooProviderConstants</c>: an admin could otherwise repoint a "Visoria" supplier's traffic at
/// an arbitrary internal host using real Visoria credentials.
/// </summary>
internal static class VisoriaProviderConstants
{
    public const string ProviderType = "Visoria";

    /// <summary>
    /// Test (<c>vsk_test_</c>) and live (<c>vsk_live_</c>) keys share this exact host — the key prefix
    /// alone determines mode, per the documentation. Same single-host, credential-differentiated shape
    /// as Bamboo.
    /// </summary>
    public const string BaseUrl = "https://api.visoria.digital";

    public const string BalancePath = "/v1/balance";
    public const string ProductsPath = "/v1/products";
    public const string ProductPathFormat = "/v1/products/{0}";
    public const string OrdersPath = "/v1/orders";
    public const string OrderPathFormat = "/v1/orders/{0}";
    public const string OrderByIdempotencyKeyPathFormat = "/v1/orders/by-idempotency-key/{0}";

    /// <summary>Visoria's documented per-page maximum for every paginated endpoint used here.</summary>
    public const int MaxPageSize = 100;
}
