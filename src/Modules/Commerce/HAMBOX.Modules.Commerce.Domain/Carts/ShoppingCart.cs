using HAMBOX.Domain.Entities;

namespace HAMBOX.Modules.Commerce.Domain.Carts;

/// <summary>
/// Represents a shopping cart for an authenticated user or guest session.
/// </summary>
public sealed class ShoppingCart : AggregateRoot
{
    private readonly List<CartItem> _items = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="ShoppingCart"/> class.
    /// </summary>
    /// <remarks>Required by EF Core.</remarks>
    private ShoppingCart()
    {
    }

    private ShoppingCart(Guid id, string? userId, string? guestSessionId)
        : base(id)
    {
        UserId = userId;
        GuestSessionId = guestSessionId;
    }

    /// <summary>
    /// Gets the authenticated user identifier, if any.
    /// </summary>
    public string? UserId { get; private set; }

    /// <summary>
    /// Gets the guest session identifier, if any.
    /// </summary>
    public string? GuestSessionId { get; private set; }

    /// <summary>
    /// Gets the coupon code applied to this cart, if any.
    /// </summary>
    public string? AppliedCouponCode { get; private set; }

    /// <summary>
    /// Gets the cart items.
    /// </summary>
    public IReadOnlyCollection<CartItem> Items => _items.AsReadOnly();

    /// <summary>
    /// Creates a shopping cart for an authenticated user.
    /// </summary>
    public static ShoppingCart CreateForUser(string userId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        return new ShoppingCart(Guid.NewGuid(), userId, null);
    }

    /// <summary>
    /// Creates a shopping cart for a guest session.
    /// </summary>
    public static ShoppingCart CreateForGuest(string guestSessionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(guestSessionId);
        return new ShoppingCart(Guid.NewGuid(), null, guestSessionId);
    }

    /// <summary>
    /// Adds or updates a product in the cart.
    /// </summary>
    public void AddOrUpdateItem(Guid productId, int quantity, decimal unitPrice, Guid? productVariantId = null)
    {
        var existing = _items.Find(i => i.ProductId == productId && i.ProductVariantId == productVariantId);
        if (existing is not null)
        {
            existing.Update(quantity, unitPrice);
            return;
        }

        _items.Add(CartItem.Create(Id, productId, quantity, unitPrice, productVariantId));
    }

    /// <summary>
    /// Updates the quantity of an existing cart item.
    /// </summary>
    public void UpdateItemQuantity(Guid productId, int quantity, decimal unitPrice, Guid? productVariantId = null)
    {
        var existing = _items.Find(i => i.ProductId == productId && i.ProductVariantId == productVariantId)
            ?? throw new InvalidOperationException($"Product '{productId}' was not found in the cart.");

        existing.Update(quantity, unitPrice);
    }

    /// <summary>
    /// Removes a product from the cart.
    /// </summary>
    public void RemoveItem(Guid productId, Guid? productVariantId = null)
    {
        var existing = _items.Find(i => i.ProductId == productId && i.ProductVariantId == productVariantId)
            ?? throw new InvalidOperationException($"Product '{productId}' was not found in the cart.");

        _items.Remove(existing);
    }

    /// <summary>
    /// Clears all items from the cart.
    /// </summary>
    public void Clear()
    {
        _items.Clear();
        AppliedCouponCode = null;
    }

    /// <summary>
    /// Applies a coupon code to the cart.
    /// </summary>
    public void ApplyCouponCode(string code)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        AppliedCouponCode = code.Trim().ToUpperInvariant();
    }

    /// <summary>
    /// Removes the applied coupon code from the cart.
    /// </summary>
    public void RemoveCouponCode() => AppliedCouponCode = null;

    /// <summary>
    /// Merges items from a guest cart into this cart.
    /// </summary>
    public void MergeFrom(ShoppingCart guestCart)
    {
        ArgumentNullException.ThrowIfNull(guestCart);

        var guestCoupon = guestCart.AppliedCouponCode;

        foreach (var guestItem in guestCart._items)
        {
            var existing = _items.Find(i => i.ProductId == guestItem.ProductId && i.ProductVariantId == guestItem.ProductVariantId);
            if (existing is not null)
            {
                existing.Update(existing.Quantity + guestItem.Quantity, guestItem.UnitPrice);
            }
            else
            {
                _items.Add(CartItem.Create(Id, guestItem.ProductId, guestItem.Quantity, guestItem.UnitPrice, guestItem.ProductVariantId));
            }
        }

        if (AppliedCouponCode is null && guestCoupon is not null)
        {
            AppliedCouponCode = guestCoupon;
        }
    }
}
