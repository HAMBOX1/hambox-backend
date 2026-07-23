using HAMBOX.Domain.Entities;

namespace HAMBOX.Modules.Catalog.Domain.Categories;

/// <summary>
/// Links a product to one of its additional (non-primary) categories.
/// </summary>
/// <remarks>
/// ProductCategory is a child entity owned by the <see cref="Products.Product"/> aggregate,
/// mirroring the <see cref="Images.ProductImage"/> ownership pattern. The product's primary
/// category is <see cref="Products.Product.CategoryId"/>; this table only ever holds additional,
/// cross-listing assignments.
/// </remarks>
public sealed class ProductCategory : Entity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ProductCategory"/> class.
    /// </summary>
    /// <remarks>Required by EF Core.</remarks>
    private ProductCategory()
    {
    }

    private ProductCategory(Guid id, Guid productId, Guid categoryId)
        : base(id)
    {
        ProductId = productId;
        CategoryId = categoryId;
    }

    /// <summary>
    /// Gets the identifier of the product this assignment belongs to.
    /// </summary>
    public Guid ProductId { get; private set; }

    /// <summary>
    /// Gets the identifier of the additional category the product is assigned to.
    /// </summary>
    public Guid CategoryId { get; private set; }

    internal static ProductCategory Create(Guid productId, Guid categoryId) =>
        new(Guid.NewGuid(), productId, categoryId);
}
