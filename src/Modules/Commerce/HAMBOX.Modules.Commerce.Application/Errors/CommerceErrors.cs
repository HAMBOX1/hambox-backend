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
}
