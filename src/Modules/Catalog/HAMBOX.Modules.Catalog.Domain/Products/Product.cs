using HAMBOX.Domain.Entities;
using HAMBOX.Modules.Catalog.Domain.Enums;
using HAMBOX.Modules.Catalog.Domain.Events;
using HAMBOX.Modules.Catalog.Domain.Images;

namespace HAMBOX.Modules.Catalog.Domain.Products;

/// <summary>
/// Represents a product in the catalog.
/// </summary>
/// <remarks>
/// Product is an aggregate root that manages its own lifecycle (Draft → Active → Inactive → Archived),
/// pricing, and image collection. Products belong to a <see cref="Categories.Category"/> and
/// support bilingual names and descriptions.
/// </remarks>
public sealed class Product : AggregateRoot, IAuditable, ISoftDeletable
{
    private readonly List<ProductImage> _images = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="Product"/> class.
    /// </summary>
    /// <remarks>Required by EF Core.</remarks>
    private Product()
    {
    }

    private Product(
        Guid id,
        string nameAr,
        string nameEn,
        string descriptionAr,
        string descriptionEn,
        decimal price,
        Guid categoryId)
        : base(id)
    {
        NameAr = nameAr;
        NameEn = nameEn;
        DescriptionAr = descriptionAr;
        DescriptionEn = descriptionEn;
        Price = price;
        CategoryId = categoryId;
        Status = ProductStatus.Draft;
    }

    /// <summary>
    /// Gets the product name in Arabic.
    /// </summary>
    public string NameAr { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the product name in English.
    /// </summary>
    public string NameEn { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the product description in Arabic.
    /// </summary>
    public string DescriptionAr { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the product description in English.
    /// </summary>
    public string DescriptionEn { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the product price.
    /// </summary>
    /// <remarks>
    /// Price must always be a non-negative value. Use <see cref="ChangePrice"/>
    /// to modify the price with proper validation and domain event emission.
    /// </remarks>
    public decimal Price { get; private set; }

    /// <summary>
    /// Gets the current lifecycle status of the product.
    /// </summary>
    public ProductStatus Status { get; private set; }

    /// <summary>
    /// Gets the identifier of the category this product belongs to.
    /// </summary>
    public Guid CategoryId { get; private set; }

    /// <summary>
    /// Gets the total stock quantity available in inventory.
    /// </summary>
    public int StockQuantity { get; private set; }

    /// <summary>
    /// Gets the quantity currently reserved for pending orders.
    /// </summary>
    public int ReservedQuantity { get; private set; }

    /// <summary>
    /// Gets the quantity available for new reservations.
    /// </summary>
    public int AvailableStock => StockQuantity - ReservedQuantity;

    /// <summary>
    /// Gets the collection of images associated with this product.
    /// </summary>
    public IReadOnlyCollection<ProductImage> Images => _images.AsReadOnly();

    /// <inheritdoc />
    public string? CreatedBy { get; private set; }

    /// <inheritdoc />
    public string? ModifiedBy { get; private set; }

    /// <inheritdoc />
    public bool IsDeleted { get; private set; }

    /// <inheritdoc />
    public DateTimeOffset? DeletedOnUtc { get; private set; }

    /// <summary>
    /// Restores a soft-deleted product.
    /// </summary>
    public void Restore()
    {
        if (!IsDeleted)
        {
            throw new InvalidOperationException("Product is not deleted.");
        }

        IsDeleted = false;
        DeletedOnUtc = null;
    }

    /// <summary>
    /// Creates a new product in <see cref="ProductStatus.Draft"/> status.
    /// </summary>
    /// <param name="nameAr">The product name in Arabic.</param>
    /// <param name="nameEn">The product name in English.</param>
    /// <param name="descriptionAr">The product description in Arabic.</param>
    /// <param name="descriptionEn">The product description in English.</param>
    /// <param name="price">The product price. Must be non-negative.</param>
    /// <param name="categoryId">The identifier of the category.</param>
    /// <returns>A new <see cref="Product"/> instance in Draft status.</returns>
    /// <remarks><paramref name="nameAr"/> and <paramref name="descriptionAr"/> are optional and fall back to the English value when left blank.</remarks>
    /// <exception cref="ArgumentException">Thrown when <paramref name="nameEn"/> or <paramref name="descriptionEn"/> is null or whitespace, or when the category identifier is empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the price is negative.</exception>
    public static Product Create(
        string nameAr,
        string nameEn,
        string descriptionAr,
        string descriptionEn,
        decimal price,
        Guid categoryId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nameEn);
        ArgumentException.ThrowIfNullOrWhiteSpace(descriptionEn);
        ArgumentOutOfRangeException.ThrowIfNegative(price);

        if (categoryId == Guid.Empty)
        {
            throw new ArgumentException("Category identifier must not be empty.", nameof(categoryId));
        }

        var product = new Product(
            Guid.NewGuid(),
            string.IsNullOrWhiteSpace(nameAr) ? nameEn : nameAr,
            nameEn,
            string.IsNullOrWhiteSpace(descriptionAr) ? descriptionEn : descriptionAr,
            descriptionEn,
            price,
            categoryId);
        product.SetInitialStock(100);
        return product;
    }

    /// <summary>
    /// Sets the initial stock quantity for a new product.
    /// </summary>
    /// <param name="quantity">The initial stock quantity.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the quantity is negative.</exception>
    public void SetInitialStock(int quantity)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(quantity);
        StockQuantity = quantity;
    }

    /// <summary>
    /// Reserves stock for a pending sale.
    /// </summary>
    /// <param name="quantity">The quantity to reserve.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the quantity is not positive.</exception>
    /// <exception cref="InvalidOperationException">Thrown when insufficient stock is available.</exception>
    public void ReserveStock(int quantity)
    {
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be greater than zero.");
        }

        if (AvailableStock < quantity)
        {
            throw new InvalidOperationException("Insufficient stock available for reservation.");
        }

        ReservedQuantity += quantity;
    }

    /// <summary>
    /// Releases a previously reserved stock quantity.
    /// </summary>
    /// <param name="quantity">The quantity to release.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the quantity is not positive.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the release exceeds reserved quantity.</exception>
    public void ReleaseReservation(int quantity)
    {
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be greater than zero.");
        }

        if (ReservedQuantity < quantity)
        {
            throw new InvalidOperationException("Cannot release more stock than is currently reserved.");
        }

        ReservedQuantity -= quantity;
    }

    /// <summary>
    /// Commits a sale by reducing both reserved and total stock quantities.
    /// </summary>
    /// <param name="quantity">The quantity sold.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the quantity is not positive.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the sale exceeds reserved quantity.</exception>
    public void CommitSale(int quantity)
    {
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be greater than zero.");
        }

        if (ReservedQuantity < quantity)
        {
            throw new InvalidOperationException("Cannot commit a sale exceeding the reserved quantity.");
        }

        ReservedQuantity -= quantity;
        StockQuantity -= quantity;
    }

    /// <summary>
    /// Updates the product details.
    /// </summary>
    /// <param name="nameAr">The new product name in Arabic.</param>
    /// <param name="nameEn">The new product name in English.</param>
    /// <param name="descriptionAr">The new product description in Arabic.</param>
    /// <param name="descriptionEn">The new product description in English.</param>
    /// <remarks><paramref name="nameAr"/> and <paramref name="descriptionAr"/> are optional and fall back to the English value when left blank.</remarks>
    /// <exception cref="ArgumentException">Thrown when <paramref name="nameEn"/> or <paramref name="descriptionEn"/> is null or whitespace.</exception>
    public void Update(
        string nameAr,
        string nameEn,
        string descriptionAr,
        string descriptionEn)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nameEn);
        ArgumentException.ThrowIfNullOrWhiteSpace(descriptionEn);

        NameAr = string.IsNullOrWhiteSpace(nameAr) ? nameEn : nameAr;
        NameEn = nameEn;
        DescriptionAr = string.IsNullOrWhiteSpace(descriptionAr) ? descriptionEn : descriptionAr;
        DescriptionEn = descriptionEn;
    }

    /// <summary>
    /// Activates the product, making it available for sale.
    /// Only products in <see cref="ProductStatus.Draft"/> or <see cref="ProductStatus.Inactive"/> status can be activated.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the product is already active or is archived.
    /// </exception>
    public void Activate()
    {
        if (Status == ProductStatus.Active)
        {
            throw new InvalidOperationException("Product is already active.");
        }

        if (Status == ProductStatus.Archived)
        {
            throw new InvalidOperationException("An archived product cannot be activated.");
        }

        Status = ProductStatus.Active;

        RaiseDomainEvent(new ProductActivatedDomainEvent(Id));
    }

    /// <summary>
    /// Deactivates the product, temporarily removing it from sale.
    /// Only products in <see cref="ProductStatus.Active"/> status can be deactivated.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the product is not active.</exception>
    public void Deactivate()
    {
        if (Status != ProductStatus.Active)
        {
            throw new InvalidOperationException("Only active products can be deactivated.");
        }

        Status = ProductStatus.Inactive;

        RaiseDomainEvent(new ProductDeactivatedDomainEvent(Id));
    }

    /// <summary>
    /// Permanently archives the product. This is an irreversible operation.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the product is already archived.</exception>
    public void Archive()
    {
        if (Status == ProductStatus.Archived)
        {
            throw new InvalidOperationException("Product is already archived.");
        }

        Status = ProductStatus.Archived;

        RaiseDomainEvent(new ProductArchivedDomainEvent(Id));
    }

    /// <summary>
    /// Changes the product price.
    /// </summary>
    /// <param name="newPrice">The new price. Must be non-negative.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the new price is negative.</exception>
    public void ChangePrice(decimal newPrice)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(newPrice);

        if (Price == newPrice)
        {
            return;
        }

        var oldPrice = Price;
        Price = newPrice;

        RaiseDomainEvent(new ProductPriceChangedDomainEvent(Id, oldPrice, newPrice));
    }

    /// <summary>
    /// Changes the category this product belongs to.
    /// </summary>
    /// <param name="categoryId">The new category identifier.</param>
    /// <exception cref="ArgumentException">Thrown when the category identifier is empty.</exception>
    public void ChangeCategory(Guid categoryId)
    {
        if (categoryId == Guid.Empty)
        {
            throw new ArgumentException("Category identifier must not be empty.", nameof(categoryId));
        }

        CategoryId = categoryId;
    }

    /// <summary>
    /// Adds an image to the product's image collection.
    /// </summary>
    /// <param name="url">The image URL.</param>
    /// <param name="displayOrder">The display order position.</param>
    /// <param name="isPrimary">Whether this is the primary image.</param>
    /// <returns>The newly created <see cref="ProductImage"/>.</returns>
    /// <exception cref="ArgumentException">Thrown when the URL is null or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the display order is negative.</exception>
    public ProductImage AddImage(
        string url,
        string storageKey,
        string fileName,
        string contentType,
        long fileSizeBytes,
        int displayOrder,
        bool isPrimary)
    {
        if (isPrimary)
        {
            foreach (var existingImage in _images)
            {
                existingImage.UnmarkAsPrimary();
            }
        }

        var image = ProductImage.Create(
            Id,
            url,
            storageKey,
            fileName,
            contentType,
            fileSizeBytes,
            displayOrder,
            isPrimary);

        _images.Add(image);

        return image;
    }

    /// <summary>
    /// Removes an image from the product's image collection.
    /// </summary>
    /// <param name="imageId">The identifier of the image to remove.</param>
    /// <exception cref="InvalidOperationException">Thrown when the image is not found.</exception>
    public void RemoveImage(Guid imageId)
    {
        var image = _images.Find(i => i.Id == imageId)
            ?? throw new InvalidOperationException($"Image with identifier '{imageId}' was not found.");

        _images.Remove(image);
    }

    /// <summary>
    /// Sets the specified image as the primary image, unmarking any existing primary.
    /// </summary>
    /// <param name="imageId">The identifier of the image to set as primary.</param>
    /// <exception cref="InvalidOperationException">Thrown when the image is not found.</exception>
    public void SetPrimaryImage(Guid imageId)
    {
        var image = _images.Find(i => i.Id == imageId)
            ?? throw new InvalidOperationException($"Image with identifier '{imageId}' was not found.");

        foreach (var existingImage in _images)
        {
            existingImage.UnmarkAsPrimary();
        }

        image.MarkAsPrimary();
    }

    /// <summary>
    /// Reorders the product images using the provided identifier sequence.
    /// </summary>
    /// <param name="orderedImageIds">The image identifiers in the desired display order.</param>
    /// <exception cref="InvalidOperationException">Thrown when the order is invalid.</exception>
    public void ReorderImages(IReadOnlyList<Guid> orderedImageIds)
    {
        if (orderedImageIds.Count != _images.Count)
        {
            throw new InvalidOperationException("All product images must be included in the reorder request.");
        }

        var imageMap = _images.ToDictionary(image => image.Id);

        for (var index = 0; index < orderedImageIds.Count; index++)
        {
            var imageId = orderedImageIds[index];

            if (!imageMap.TryGetValue(imageId, out var image))
            {
                throw new InvalidOperationException($"Image with identifier '{imageId}' was not found.");
            }

            image.UpdateDisplayOrder(index);
        }
    }
}
