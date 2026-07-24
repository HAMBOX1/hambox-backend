using HAMBOX.Domain.Entities;

namespace HAMBOX.Modules.Catalog.Domain.Collections;

/// <summary>
/// Links a product to a collection.
/// </summary>
/// <remarks>
/// ProductCollectionItem is a child entity owned by the <see cref="Products.Product"/> aggregate,
/// mirroring the <see cref="Categories.ProductCategory"/> ownership pattern. Unlike categories,
/// collections have no "primary" concept — a product may belong to any number of collections.
/// </remarks>
public sealed class ProductCollectionItem : Entity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ProductCollectionItem"/> class.
    /// </summary>
    /// <remarks>Required by EF Core.</remarks>
    private ProductCollectionItem()
    {
    }

    private ProductCollectionItem(Guid id, Guid productId, Guid collectionId)
        : base(id)
    {
        ProductId = productId;
        CollectionId = collectionId;
    }

    /// <summary>
    /// Gets the identifier of the product this assignment belongs to.
    /// </summary>
    public Guid ProductId { get; private set; }

    /// <summary>
    /// Gets the identifier of the collection the product is assigned to.
    /// </summary>
    public Guid CollectionId { get; private set; }

    internal static ProductCollectionItem Create(Guid productId, Guid collectionId) =>
        new(Guid.NewGuid(), productId, collectionId);
}
