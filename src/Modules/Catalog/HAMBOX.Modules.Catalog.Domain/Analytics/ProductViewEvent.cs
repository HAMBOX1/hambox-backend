using HAMBOX.Domain.Entities;

namespace HAMBOX.Modules.Catalog.Domain.Analytics;

/// <summary>
/// Best-effort log of a storefront product view for analytics.
/// </summary>
public sealed class ProductViewEvent : Entity
{
    private ProductViewEvent()
    {
    }

    private ProductViewEvent(Guid id, Guid productId, string? userId)
        : base(id)
    {
        ProductId = productId;
        UserId = userId;
        CreatedOnUtc = DateTimeOffset.UtcNow;
    }

    public Guid ProductId { get; private set; }
    public string? UserId { get; private set; }

    public static ProductViewEvent Create(Guid productId, string? userId = null)
    {
        if (productId == Guid.Empty)
        {
            throw new ArgumentException("Product id is required.", nameof(productId));
        }

        return new ProductViewEvent(
            Guid.NewGuid(),
            productId,
            string.IsNullOrWhiteSpace(userId) ? null : userId.Trim());
    }
}
