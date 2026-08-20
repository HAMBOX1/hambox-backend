namespace HAMBOX.Modules.Messaging.Application.Services;

/// <summary>
/// Builds storefront deep links from the same <c>ApplicationBaseUrl</c> Platform Setting the email
/// templates already use (<c>EmailSettingsPayload.ApplicationBaseUrl</c>) — not a new setting, since
/// this is the same "where does the website live" value, just consumed by chat replies instead of
/// emails. Routes mirror the frontend's actual paths (see <c>features/products</c>, <c>features/cart</c>,
/// <c>features/checkout</c> routing).
/// </summary>
public static class WhatsAppDeepLinkBuilder
{
    public static string ProductUrl(string applicationBaseUrl, Guid productId) =>
        $"{applicationBaseUrl.TrimEnd('/')}/products/{productId}";

    public static string CartUrl(string applicationBaseUrl) =>
        $"{applicationBaseUrl.TrimEnd('/')}/cart";

    public static string CheckoutUrl(string applicationBaseUrl) =>
        $"{applicationBaseUrl.TrimEnd('/')}/checkout";
}
