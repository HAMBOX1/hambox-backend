using System.Security.Claims;

namespace HAMBOX.Modules.Commerce.Presentation.Endpoints;

/// <summary>
/// Checkout always runs as a signed-in customer, so the order's contact address is the account's own e-mail,
/// never a free-text value from the request: otherwise a receipt (and the admin's view of "who bought this")
/// could be pointed at any third party.
/// </summary>
internal static class CheckoutEmail
{
    public static string Resolve(ClaimsPrincipal user, string requested) =>
        user.FindFirst(ClaimTypes.Email)?.Value
        ?? user.FindFirst("email")?.Value
        ?? requested;
}
