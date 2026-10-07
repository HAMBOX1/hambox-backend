namespace HAMBOX.Application.Variants;

/// <summary>
/// Lets Catalog's merge flow keep Commerce's operational rows (open cart lines, back-in-stock alert
/// subscriptions) pointing at the product a variant now belongs to. Order history is deliberately left
/// untouched. Lives in BuildingBlocks for the same reason as <see cref="ICommerceVariantUsageProvider"/>.
/// </summary>
public interface ICommerceVariantRelocator
{
    /// <summary>Re-points cart lines and alert subscriptions of the variant at <paramref name="newProductId"/>. Idempotent.</summary>
    Task MoveVariantAsync(Guid variantId, Guid newProductId, CancellationToken cancellationToken = default);
}
