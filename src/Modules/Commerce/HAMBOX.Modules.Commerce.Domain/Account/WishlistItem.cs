using HAMBOX.Domain.Entities;

namespace HAMBOX.Modules.Commerce.Domain.Account;

/// <summary>
/// Represents a product saved to a user's wishlist.
/// </summary>
public sealed class WishlistItem : Entity
{
    private WishlistItem()
    {
    }

    private WishlistItem(Guid id, string userId, Guid productId, DateTimeOffset addedOnUtc)
        : base(id)
    {
        UserId = userId;
        ProductId = productId;
        AddedOnUtc = addedOnUtc;
    }

    /// <summary>
    /// Gets the user identifier.
    /// </summary>
    public string UserId { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the product identifier.
    /// </summary>
    public Guid ProductId { get; private set; }

    /// <summary>
    /// Gets the date and time the product was added to the wishlist.
    /// </summary>
    public DateTimeOffset AddedOnUtc { get; private set; }

    /// <summary>
    /// Creates a new wishlist item.
    /// </summary>
    public static WishlistItem Create(string userId, Guid productId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        if (productId == Guid.Empty)
        {
            throw new ArgumentException("Product identifier must not be empty.", nameof(productId));
        }

        return new WishlistItem(Guid.NewGuid(), userId, productId, DateTimeOffset.UtcNow);
    }
}
