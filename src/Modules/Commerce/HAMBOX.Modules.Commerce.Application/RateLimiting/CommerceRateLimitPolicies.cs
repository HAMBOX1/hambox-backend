namespace HAMBOX.Modules.Commerce.Application.RateLimiting;

/// <summary>
/// Named ASP.NET Core rate limiting policies for Commerce endpoints — mirrors
/// <c>HAMBOX.Modules.Identity.Application.RateLimiting.RateLimitPolicies</c>'s pattern for the same
/// reason: a fixed-window, per-client-IP defense-in-depth layer on abuse-prone endpoints, additive
/// to (not a replacement for) the server-side payment/idempotency checks that already gate these paths.
/// </summary>
public static class CommerceRateLimitPolicies
{
    /// <summary>Applied to checkout-initiation endpoints (generic checkout, membership, DOT, DOT-Fawry).</summary>
    public const string CheckoutInitiation = "commerce:checkout-initiation";

    /// <summary>Applied to payment provider webhook/notify callbacks (DOT, DOT-Fawry).</summary>
    public const string PaymentCallback = "commerce:payment-callback";
}
