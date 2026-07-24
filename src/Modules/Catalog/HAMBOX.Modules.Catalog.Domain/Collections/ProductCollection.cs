using HAMBOX.Domain.Entities;

namespace HAMBOX.Modules.Catalog.Domain.Collections;

/// <summary>
/// Represents an internal, owner-only organizational label for products.
/// </summary>
/// <remarks>
/// Unlike <see cref="Categories.Category"/>, collections never affect the storefront, navigation,
/// SEO, or customer-facing filters — they exist purely to help the owner organize products (e.g.
/// by supplier, campaign, or operational status). Collections support nesting via <see cref="ParentId"/>,
/// the same shape as <see cref="Categories.Category"/>.
/// </remarks>
public sealed class ProductCollection : AggregateRoot, IAuditable, ISoftDeletable
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ProductCollection"/> class.
    /// </summary>
    /// <remarks>Required by EF Core.</remarks>
    private ProductCollection()
    {
    }

    private ProductCollection(
        Guid id,
        string name,
        string? description,
        string? color,
        string? icon,
        bool isSystem)
        : base(id)
    {
        Name = name;
        Description = description;
        Color = color;
        Icon = icon;
        IsSystem = isSystem;
    }

    /// <summary>
    /// Gets the collection name.
    /// </summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the optional description.
    /// </summary>
    public string? Description { get; private set; }

    /// <summary>
    /// Gets the optional display color (hex string).
    /// </summary>
    public string? Color { get; private set; }

    /// <summary>
    /// Gets the optional display icon (PrimeIcons class name).
    /// </summary>
    public string? Icon { get; private set; }

    /// <summary>
    /// Gets the optional parent collection identifier.
    /// </summary>
    public Guid? ParentId { get; private set; }

    /// <summary>
    /// Gets the display order among sibling collections (same <see cref="ParentId"/>).
    /// </summary>
    public int SortOrder { get; private set; }

    /// <summary>
    /// Gets a value indicating whether this collection is system-defined and cannot be deleted.
    /// </summary>
    public bool IsSystem { get; private set; }

    /// <inheritdoc />
    public string? CreatedBy { get; private set; }

    /// <inheritdoc />
    public string? ModifiedBy { get; private set; }

    /// <inheritdoc />
    public bool IsDeleted { get; private set; }

    /// <inheritdoc />
    public DateTimeOffset? DeletedOnUtc { get; private set; }

    /// <summary>
    /// Creates a new collection.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is null or whitespace.</exception>
    public static ProductCollection Create(
        string name,
        string? description = null,
        string? color = null,
        string? icon = null,
        Guid? parentId = null,
        int sortOrder = 0,
        bool isSystem = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var collection = new ProductCollection(Guid.NewGuid(), name, description, color, icon, isSystem);
        collection.SetParent(parentId);
        collection.SetSortOrder(sortOrder);
        return collection;
    }

    /// <summary>
    /// Restores a soft-deleted collection.
    /// </summary>
    public void Restore()
    {
        if (!IsDeleted)
        {
            throw new InvalidOperationException("Collection is not deleted.");
        }

        IsDeleted = false;
        DeletedOnUtc = null;
    }

    /// <summary>
    /// Updates the collection details.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is null or whitespace.</exception>
    public void Update(
        string name,
        string? description,
        string? color,
        string? icon,
        Guid? parentId,
        int sortOrder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Name = name;
        Description = description;
        Color = color;
        Icon = icon;
        SetParent(parentId);
        SetSortOrder(sortOrder);
    }

    /// <summary>
    /// Sets the parent collection.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when a collection is set as its own parent.</exception>
    public void SetParent(Guid? parentId)
    {
        if (parentId == Id)
        {
            throw new InvalidOperationException("A collection cannot be its own parent.");
        }

        ParentId = parentId;
    }

    /// <summary>
    /// Sets the display order among sibling collections.
    /// </summary>
    public void SetSortOrder(int sortOrder)
    {
        SortOrder = sortOrder;
    }
}
