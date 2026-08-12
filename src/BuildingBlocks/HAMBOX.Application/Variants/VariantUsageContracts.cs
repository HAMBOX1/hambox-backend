namespace HAMBOX.Application.Variants;

/// <summary>
/// Commerce-side reference counts for a single Catalog product variant. Backed by Commerce's
/// CartItems/OrderItems/OrderLicenseKeys tables (bare, unenforced VariantId columns — Catalog and
/// Commerce are separate schemas with no cross-schema FKs).
/// </summary>
public sealed record CommerceVariantUsageSnapshot(
    int CartItemCount,
    int OrderItemCount,
    int OrderLicenseKeyCount);

/// <summary>
/// Lets Catalog's variant usage-inspection/deletion flow learn about Commerce-side references
/// without Catalog taking a project reference to Commerce — the existing module dependency
/// direction is Commerce -&gt; Catalog, never the reverse, so this contract lives in BuildingBlocks
/// instead. Implemented in Commerce.Application, registered in Commerce's DI extension; mirrors
/// how ICommunicationService and IMembershipAccessProvider let modules depend on a BuildingBlocks
/// abstraction instead of a concrete sibling module.
/// </summary>
public interface ICommerceVariantUsageProvider
{
    Task<CommerceVariantUsageSnapshot> GetUsageAsync(Guid variantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes every CartItem referencing this variant — cart lines are purely operational,
    /// never order history, so this is always safe. Returns the number removed; safe to call
    /// more than once (a second call simply finds nothing left).
    /// </summary>
    Task<int> RemoveCartItemsAsync(Guid variantId, CancellationToken cancellationToken = default);
}
