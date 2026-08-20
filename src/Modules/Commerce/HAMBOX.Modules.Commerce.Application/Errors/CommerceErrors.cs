using HAMBOX.SharedKernel.Errors;

namespace HAMBOX.Modules.Commerce.Application.Errors;

/// <summary>
/// Defines predefined errors for the Commerce module.
/// </summary>
public static class CommerceErrors
{
    /// <summary>
    /// Gets the error for when a cart is not found.
    /// </summary>
    public static readonly Error CartNotFound = new(
        "Cart.NotFound",
        "The shopping cart was not found.");

    /// <summary>
    /// Gets the error for when a cart item is not found.
    /// </summary>
    public static readonly Error CartItemNotFound = new(
        "Cart.ItemNotFound",
        "The specified product was not found in the cart.");

    /// <summary>
    /// Gets the error for when the cart is empty.
    /// </summary>
    public static readonly Error CartEmpty = new(
        "Cart.Empty",
        "The shopping cart is empty.");

    /// <summary>
    /// Gets the error for when a product is not found in the catalog.
    /// </summary>
    public static readonly Error ProductNotFound = new(
        "Cart.ProductNotFound",
        "The specified product was not found.");

    /// <summary>
    /// Gets the error for when checkout requires authentication.
    /// </summary>
    public static readonly Error AuthenticationRequired = new(
        "Checkout.AuthenticationRequired",
        "Authentication is required to complete checkout.");

    /// <summary>
    /// Gets the error for when an order is not found.
    /// </summary>
    public static readonly Error OrderNotFound = new(
        "Orders.NotFound",
        "The order with the specified identifier was not found.");

    /// <summary>
    /// Gets the error for when a guest session identifier is required.
    /// </summary>
    public static readonly Error GuestSessionRequired = new(
        "Cart.GuestSessionRequired",
        "A guest session identifier is required for this operation.");

    /// <summary>
    /// Gets the error for when a wishlist item is not found.
    /// </summary>
    public static readonly Error WishlistItemNotFound = new(
        "Wishlist.NotFound",
        "The specified product was not found in the wishlist.");

    /// <summary>
    /// Gets the error for when a wishlist item already exists.
    /// </summary>
    public static readonly Error WishlistItemExists = new(
        "Wishlist.AlreadyExists",
        "The specified product is already in the wishlist.");

    /// <summary>
    /// Gets the error for when an alert subscription is not found (or does not belong to the caller —
    /// deliberately the same error either way, matching the Wishlist ownership idiom of never
    /// revealing that a differently-owned row exists).
    /// </summary>
    public static readonly Error AlertSubscriptionNotFound = new(
        "CustomerAlerts.NotFound",
        "The specified alert subscription was not found.");

    /// <summary>
    /// Gets the error for when an identical active alert subscription already exists.
    /// </summary>
    public static readonly Error AlertSubscriptionExists = new(
        "CustomerAlerts.AlreadyExists",
        "You already have an active alert for this product and variant.");

    /// <summary>
    /// Gets the error for when neither an authenticated user nor a guest session identifier is
    /// available to own a new alert subscription.
    /// </summary>
    public static readonly Error AlertSubscriptionOwnerRequired = new(
        "CustomerAlerts.OwnerRequired",
        "Sign in or provide a guest session to create an alert.");

    /// <summary>
    /// Gets the error for subscribing to a back-in-stock alert on a variant that is already
    /// purchasable — there is no "became available" transition left to notify on.
    /// </summary>
    public static readonly Error VariantAlreadyAvailable = new(
        "CustomerAlerts.VariantAlreadyAvailable",
        "This variant is already available to purchase.");

    /// <summary>
    /// Gets the error for when a review is not found.
    /// </summary>
    public static readonly Error ReviewNotFound = new(
        "Reviews.NotFound",
        "The review with the specified identifier was not found.");

    /// <summary>
    /// Gets the error for when a review already exists.
    /// </summary>
    public static readonly Error ReviewAlreadyExists = new(
        "Reviews.AlreadyExists",
        "You have already reviewed this product.");

    /// <summary>
    /// Gets the error for when a review cannot be created without a verified purchase.
    /// </summary>
    public static readonly Error ReviewNotVerifiedPurchase = new(
        "Reviews.NotVerifiedPurchase",
        "You must purchase this product in a completed order before leaving a review.");

    /// <summary>
    /// Gets the error for when a notification is not found.
    /// </summary>
    public static readonly Error NotificationNotFound = new(
        "Notifications.NotFound",
        "The notification with the specified identifier was not found.");

    public static Error InvalidCoupon(string message) => new("Promotions.InvalidCoupon", message);

    public static readonly Error PromotionNotFound = new(
        "Promotions.NotFound",
        "The promotion was not found.");

    public static readonly Error CouponNotFound = new(
        "Coupons.NotFound",
        "The coupon code was not found.");

    public static readonly Error MembershipPlanNotFound = new("Memberships.PlanNotFound", "Membership plan was not found.");
    public static readonly Error MembershipSubscriptionNotFound = new("Memberships.SubscriptionNotFound", "Membership subscription was not found.");
    public static readonly Error MembershipSubscriptionAlreadyActive = new("Memberships.SubscriptionAlreadyActive", "You already have an active membership. Use upgrade or downgrade instead.");
    public static readonly Error MembershipPlanSlugExists = new("Memberships.SlugExists", "A plan with this slug already exists.");
    public static readonly Error MembershipCheckoutRequired = new("Memberships.CheckoutRequired", "Complete membership purchase through checkout.");
    public static readonly Error MembershipCheckoutActionInvalid = new("Memberships.CheckoutActionInvalid", "The membership checkout action is not supported.");
    public static readonly Error MembershipCheckoutPending = new("Memberships.CheckoutPending", "A membership checkout is already pending. Complete or retry payment.");
    public static readonly Error MembershipPlanUnchanged = new("Memberships.PlanUnchanged", "You are already on this membership plan.");

    public static Error ProductMembersOnly(IReadOnlyCollection<string> requiredPlanNames) => new(
        "Cart.ProductMembersOnly",
        requiredPlanNames.Count > 0
            ? $"This product is exclusive to {string.Join(" or ", requiredPlanNames)} members."
            : "This product is exclusive to members.");

    public static Error ProductNotYetReleased(DateTime publicReleaseOnUtc) => new(
        "Cart.ProductNotYetReleased",
        $"This product is not yet available. It releases on {publicReleaseOnUtc:yyyy-MM-dd}.");

    public static Error MembershipPurchaseLimitExceeded(int limit) => new(
        "Checkout.MembershipPurchaseLimitExceeded",
        $"Your membership plan allows up to {limit} purchase(s) per month. You've reached that limit for this month.");

    public static readonly Error PaymentMethodNotSupported = new(
        "Checkout.PaymentMethodNotSupported",
        "The selected payment method is not supported.");

    public static readonly Error PaymentFailed = new(
        "Checkout.PaymentFailed",
        "Payment could not be processed.");

    public static Error InvalidOrderStatus(string status) => new(
        "Orders.InvalidStatus",
        $"The order status '{status}' is not supported.");

    public static Error OrderStatusTransitionFailed(string message) => new(
        "Orders.StatusTransitionFailed",
        message);

    public static readonly Error OrderNoteNotFound = new(
        "Orders.NoteNotFound",
        "The admin note was not found.");

    public static readonly Error OrderLicenseKeyNotFound = new(
        "Orders.LicenseKeyNotFound",
        "The license key was not found.");

    public static readonly Error OrderRefundNotSupported = new(
        "Orders.RefundNotSupported",
        "This order cannot be refunded.");

    public static readonly Error OrderItemNotFound = new(
        "Orders.ItemNotFound",
        "The order line item was not found.");

    public static readonly Error OrderFulfillmentFailed = new(
        "Orders.FulfillmentFailed",
        "Fulfillment could not be completed.");

    public static readonly Error OrderFulfillmentNothingToRetry = new(
        "Orders.FulfillmentNothingToRetry",
        "All line items already have the required digital codes.");

    public static readonly Error OrderBulkEmpty = new(
        "Orders.BulkEmpty",
        "Select at least one order for bulk actions.");

    public static Error OrderBulkActionNotSupported(string action) => new(
        "Orders.BulkActionNotSupported",
        $"Bulk action '{action}' is not supported.");

    public static Error OrderManualCodeInvalid(string message) => new(
        "Orders.ManualCodeInvalid",
        message);

    /// <summary>
    /// Gets the single, deliberately non-specific error for every reason a customer may not view a
    /// product's instructions (order not found, not theirs, not completed, or instructions not
    /// published) — collapsed to one code so the 403 response never reveals which check failed.
    /// </summary>
    public static readonly Error InstructionsNotAccessible = new(
        "Library.InstructionsNotAccessible",
        "These instructions are not available.");

    /// <summary>
    /// Gets the error returned when DOT checkout is attempted before the client has confirmed
    /// DOT's pricing model (fixed price point vs. arbitrary amount) for the configured service_id.
    /// See <c>IDotPricePointResolver</c>.
    /// </summary>
    public static readonly Error DotPricingNotConfigured = new(
        "Dot.PricingNotConfigured",
        "This payment method is not yet available.");

    public static readonly Error DotGatewayMisconfigured = new(
        "Dot.GatewayMisconfigured",
        "This payment method is not yet available.");

    public static readonly Error DotPaymentAttemptNotFound = new(
        "Dot.PaymentAttemptNotFound",
        "The payment attempt was not found.");

    /// <summary>
    /// Gets the single, deliberately non-specific error for every way a DOT callback/notification
    /// fails to correspond to a known, still-open payment attempt (unknown partner_txid, wrong
    /// operator/service context, already-finalized attempt) — collapsed to one code so the response
    /// never reveals which check failed to an unauthenticated caller.
    /// </summary>
    public static readonly Error DotCallbackInvalid = new(
        "Dot.CallbackInvalid",
        "The payment callback could not be processed.");

    public static readonly Error DotVerificationFailed = new(
        "Dot.VerificationFailed",
        "The payment could not be verified.");

    public static readonly Error DotProviderUnavailable = new(
        "Dot.ProviderUnavailable",
        "The payment provider is temporarily unavailable. Please try again shortly.");

    /// <summary>
    /// Gets the error returned when DOT Fawry checkout is attempted before the client has confirmed
    /// the currency DOT expects for the configured Fawry service_id. See
    /// <c>IDotFawryChargeAmountResolver</c>. A distinct DOT product from carrier-billing OTP
    /// (<see cref="DotPricingNotConfigured"/>) — do not conflate the two.
    /// </summary>
    public static readonly Error DotFawryPricingNotConfigured = new(
        "DotFawry.PricingNotConfigured",
        "This payment method is not yet available.");

    public static readonly Error DotFawryGatewayMisconfigured = new(
        "DotFawry.GatewayMisconfigured",
        "This payment method is not yet available.");

    public static readonly Error DotFawryPaymentAttemptNotFound = new(
        "DotFawry.PaymentAttemptNotFound",
        "The payment attempt was not found.");

    /// <summary>
    /// Gets the single, deliberately non-specific error for every way a DOT Fawry notification
    /// fails to correspond to a known, still-open payment attempt — collapsed to one code so the
    /// response never reveals which check failed to an unauthenticated caller.
    /// </summary>
    public static readonly Error DotFawryNotificationInvalid = new(
        "DotFawry.NotificationInvalid",
        "The payment notification could not be processed.");

    public static readonly Error DotFawryVerificationFailed = new(
        "DotFawry.VerificationFailed",
        "The payment could not be verified.");

    public static readonly Error DotFawryProviderUnavailable = new(
        "DotFawry.ProviderUnavailable",
        "The payment provider is temporarily unavailable. Please try again shortly.");
}
