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
}
