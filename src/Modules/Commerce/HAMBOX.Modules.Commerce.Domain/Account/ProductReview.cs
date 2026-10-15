using HAMBOX.Domain.Entities;

namespace HAMBOX.Modules.Commerce.Domain.Account;

/// <summary>
/// Represents a verified product review from a customer.
/// </summary>
public sealed class ProductReview : Entity
{
    private ProductReview()
    {
    }

    private ProductReview(
        Guid id,
        string userId,
        Guid productId,
        Guid orderId,
        int rating,
        string comment)
        : base(id)
    {
        UserId = userId;
        ProductId = productId;
        OrderId = orderId;
        Rating = rating;
        Comment = comment;
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
    /// Gets the order identifier used to verify the purchase.
    /// </summary>
    public Guid OrderId { get; private set; }

    /// <summary>
    /// Gets the rating from 1 to 5.
    /// </summary>
    public int Rating { get; private set; }

    /// <summary>
    /// Gets the review comment.
    /// </summary>
    public string Comment { get; private set; } = string.Empty;

    /// <summary>True while a moderator has taken this review off the storefront.</summary>
    public bool IsHidden { get; private set; }

    public string? HiddenReason { get; private set; }

    public string? HiddenByUserId { get; private set; }

    public DateTimeOffset? HiddenOnUtc { get; private set; }

    /// <summary>Takes the review off the public storefront (it stays visible to its author and to admins).</summary>
    public void Hide(string moderatorUserId, string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moderatorUserId);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        IsHidden = true;
        HiddenReason = reason.Length > 500 ? reason[..500] : reason;
        HiddenByUserId = moderatorUserId;
        HiddenOnUtc = DateTimeOffset.UtcNow;
    }

    public void Unhide()
    {
        IsHidden = false;
        HiddenReason = null;
        HiddenByUserId = null;
        HiddenOnUtc = null;
    }

    /// <summary>
    /// Creates a new product review.
    /// </summary>
    public static ProductReview Create(
        string userId,
        Guid productId,
        Guid orderId,
        int rating,
        string comment)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(comment);

        if (productId == Guid.Empty)
        {
            throw new ArgumentException("Product identifier must not be empty.", nameof(productId));
        }

        if (orderId == Guid.Empty)
        {
            throw new ArgumentException("Order identifier must not be empty.", nameof(orderId));
        }

        if (rating is < 1 or > 5)
        {
            throw new ArgumentOutOfRangeException(nameof(rating), "Rating must be between 1 and 5.");
        }

        if (comment.Length > 2000)
        {
            throw new ArgumentException("Comment must not exceed 2000 characters.", nameof(comment));
        }

        return new ProductReview(Guid.NewGuid(), userId, productId, orderId, rating, comment);
    }

    /// <summary>
    /// Updates the review rating and comment.
    /// </summary>
    public void Update(int rating, string comment)
    {
        if (rating is < 1 or > 5)
        {
            throw new ArgumentOutOfRangeException(nameof(rating), "Rating must be between 1 and 5.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(comment);

        if (comment.Length > 2000)
        {
            throw new ArgumentException("Comment must not exceed 2000 characters.", nameof(comment));
        }

        Rating = rating;
        Comment = comment;
    }
}
