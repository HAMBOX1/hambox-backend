using HAMBOX.Domain.Entities;
using HAMBOX.Modules.Catalog.Domain.Categories;
using HAMBOX.Modules.Catalog.Domain.Collections;
using HAMBOX.Modules.Catalog.Domain.Enums;
using HAMBOX.Modules.Catalog.Domain.Events;
using HAMBOX.Modules.Catalog.Domain.Images;

namespace HAMBOX.Modules.Catalog.Domain.Products;

/// <summary>
/// Represents a product in the catalog.
/// </summary>
/// <remarks>
/// Product is an aggregate root that manages its own lifecycle (Draft → Active → Inactive → Archived),
/// pricing, and image collection. Every product has exactly one primary <see cref="Categories.Category"/>
/// (<see cref="CategoryId"/>) plus zero or more additional, cross-listing categories, and
/// support bilingual names and descriptions.
/// </remarks>
public sealed class Product : AggregateRoot, IAuditable, ISoftDeletable
{
    private readonly List<ProductImage> _images = [];
    private readonly List<ProductCategory> _additionalCategories = [];
    private readonly List<ProductCollectionItem> _collections = [];

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
    /// Gets the date the product becomes available to the general public. Null means it is
    /// public as soon as it's <see cref="ProductStatus.Active"/> (the default for every existing
    /// product). When set in the future, members whose plan grants an EarlyAccess benefit may
    /// still purchase during their lead-time window; everyone else sees a release countdown.
    /// </summary>
    public DateTime? PublicReleaseOnUtc { get; private set; }

    /// <summary>
    /// Gets the identifier of the primary category this product belongs to.
    /// </summary>
    /// <remarks>
    /// The primary category drives the canonical storefront URL, breadcrumbs, default
    /// navigation, and SEO metadata. See <see cref="AdditionalCategories"/> for cross-listing.
    /// </remarks>
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

    /// <summary>
    /// Gets the additional (non-primary) categories this product is cross-listed under.
    /// Used for browsing, search, discovery, and promotions alongside <see cref="CategoryId"/>.
    /// </summary>
    public IReadOnlyCollection<ProductCategory> AdditionalCategories => _additionalCategories.AsReadOnly();

    /// <summary>
    /// Gets the internal, owner-only collections this product is organized under.
    /// </summary>
    /// <remarks>
    /// Collections are independent of <see cref="CategoryId"/>/<see cref="AdditionalCategories"/> —
    /// they never affect the storefront and have no "primary" concept.
    /// </remarks>
    public IReadOnlyCollection<ProductCollectionItem> Collections => _collections.AsReadOnly();

    /// <inheritdoc />
    public string? CreatedBy { get; private set; }

    /// <inheritdoc />
    public string? ModifiedBy { get; private set; }

    /// <summary>
    /// Gets the identifier of the admin who last edited this product's own fields (name, price,
    /// category, status, etc.) via the admin catalog UI.
    /// </summary>
    /// <remarks>
    /// Unlike <see cref="ModifiedBy"/>, which is bumped by any save including checkout-driven stock
    /// reservation, this is only set by <see cref="RecordAdminEdit"/> so it reliably answers "which
    /// admin last edited this record" rather than "who last touched any field."
    /// </remarks>
    public string? LastEditedByUserId { get; private set; }

    /// <summary>
    /// Gets the display name (email) of the admin who last edited this product. See <see cref="LastEditedByUserId"/>.
    /// </summary>
    public string? LastEditedByName { get; private set; }

    /// <summary>
    /// Gets when the product was last edited by an admin. See <see cref="LastEditedByUserId"/>.
    /// </summary>
    public DateTimeOffset? LastEditedOnUtc { get; private set; }

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
    /// <remarks><paramref name="nameAr"/>, <paramref name="descriptionAr"/>, and <paramref name="descriptionEn"/> are optional; <paramref name="descriptionAr"/> falls back to the English value when left blank.</remarks>
    /// <exception cref="ArgumentException">Thrown when <paramref name="nameEn"/> is null or whitespace, or when the category identifier is empty.</exception>
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
    /// Restores previously committed stock, e.g. when the order that sold it is cancelled or refunded.
    /// </summary>
    /// <param name="quantity">The quantity to restore.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the quantity is not positive.</exception>
    public void RestoreStock(int quantity)
    {
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be greater than zero.");
        }

        StockQuantity += quantity;
    }

    /// <summary>
    /// Updates the product details.
    /// </summary>
    /// <param name="nameAr">The new product name in Arabic.</param>
    /// <param name="nameEn">The new product name in English.</param>
    /// <param name="descriptionAr">The new product description in Arabic.</param>
    /// <param name="descriptionEn">The new product description in English.</param>
    /// <remarks><paramref name="nameAr"/>, <paramref name="descriptionAr"/>, and <paramref name="descriptionEn"/> are optional; <paramref name="descriptionAr"/> falls back to the English value when left blank.</remarks>
    /// <exception cref="ArgumentException">Thrown when <paramref name="nameEn"/> is null or whitespace.</exception>
    public void Update(
        string nameAr,
        string nameEn,
        string descriptionAr,
        string descriptionEn)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nameEn);

        NameAr = string.IsNullOrWhiteSpace(nameAr) ? nameEn : nameAr;
        NameEn = nameEn;
        DescriptionAr = string.IsNullOrWhiteSpace(descriptionAr) ? descriptionEn : descriptionAr;
        DescriptionEn = descriptionEn;
    }

    /// <summary>
    /// Records that an admin edited this product's own fields, for "last edited by" display.
    /// </summary>
    /// <param name="userId">The editing admin's user identifier.</param>
    /// <param name="displayName">The editing admin's display name (email).</param>
    public void RecordAdminEdit(string? userId, string? displayName)
    {
        LastEditedByUserId = userId;
        LastEditedByName = displayName;
        LastEditedOnUtc = DateTimeOffset.UtcNow;
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
    /// Sets or clears the product's public release date. Pass null to make it public
    /// immediately (subject to <see cref="Status"/> being Active).
    /// </summary>
    public void ScheduleRelease(DateTime? publicReleaseOnUtc) => PublicReleaseOnUtc = publicReleaseOnUtc;

    /// <summary>
    /// Changes the primary category this product belongs to.
    /// </summary>
    /// <param name="categoryId">The new primary category identifier.</param>
    /// <exception cref="ArgumentException">Thrown when the category identifier is empty.</exception>
    /// <remarks>
    /// If <paramref name="categoryId"/> was already assigned as an additional category, it is
    /// dropped from that set — a category can never be both the primary and an additional
    /// category at the same time.
    /// </remarks>
    public void ChangeCategory(Guid categoryId)
    {
        if (categoryId == Guid.Empty)
        {
            throw new ArgumentException("Category identifier must not be empty.", nameof(categoryId));
        }

        CategoryId = categoryId;
        _additionalCategories.RemoveAll(pc => pc.CategoryId == categoryId);
    }

    /// <summary>
    /// Replaces the full set of additional (non-primary) categories this product is
    /// cross-listed under.
    /// </summary>
    /// <param name="categoryIds">The additional category identifiers. Duplicates are ignored.</param>
    /// <returns>
    /// The newly created <see cref="ProductCategory"/> rows. When <see cref="Product"/> is already
    /// tracked by EF Core (e.g. loaded for an update, as opposed to a brand-new aggregate), adding
    /// these via the collection navigation alone isn't enough for EF to recognize them as new rows —
    /// the caller must explicitly add them to <c>ICatalogDbContext.ProductCategories</c>, mirroring
    /// how <see cref="AddImage"/> callers explicitly add to <c>ProductImages</c>.
    /// </returns>
    /// <exception cref="ArgumentException">Thrown when a category id duplicates the primary category.</exception>
    public IReadOnlyList<ProductCategory> SetAdditionalCategories(IEnumerable<Guid> categoryIds)
    {
        var distinctIds = categoryIds.Distinct().ToList();

        if (distinctIds.Contains(CategoryId))
        {
            throw new ArgumentException(
                "A category cannot be assigned as both the primary and an additional category.",
                nameof(categoryIds));
        }

        _additionalCategories.RemoveAll(pc => !distinctIds.Contains(pc.CategoryId));

        var existingIds = _additionalCategories.Select(pc => pc.CategoryId).ToHashSet();
        var added = new List<ProductCategory>();

        foreach (var categoryId in distinctIds.Where(id => !existingIds.Contains(id)))
        {
            var productCategory = ProductCategory.Create(Id, categoryId);
            _additionalCategories.Add(productCategory);
            added.Add(productCategory);
        }

        return added;
    }

    /// <summary>
    /// Replaces the full set of collections this product is organized under.
    /// </summary>
    /// <param name="collectionIds">The collection identifiers. Duplicates are ignored.</param>
    /// <returns>
    /// The newly created <see cref="ProductCollectionItem"/> rows. When <see cref="Product"/> is
    /// already tracked by EF Core, the caller must explicitly add these to
    /// <c>ICatalogDbContext.ProductCollectionItems</c>, mirroring <see cref="SetAdditionalCategories"/>.
    /// </returns>
    public IReadOnlyList<ProductCollectionItem> SetCollections(IEnumerable<Guid> collectionIds)
    {
        var distinctIds = collectionIds.Distinct().ToList();

        _collections.RemoveAll(pc => !distinctIds.Contains(pc.CollectionId));

        var existingIds = _collections.Select(pc => pc.CollectionId).ToHashSet();
        var added = new List<ProductCollectionItem>();

        foreach (var collectionId in distinctIds.Where(id => !existingIds.Contains(id)))
        {
            var productCollectionItem = ProductCollectionItem.Create(Id, collectionId);
            _collections.Add(productCollectionItem);
            added.Add(productCollectionItem);
        }

        return added;
    }

    /// <summary>
    /// Adds this product to a single collection, if it isn't already a member.
    /// </summary>
    /// <returns>The newly created <see cref="ProductCollectionItem"/>, or <c>null</c> if already a member.</returns>
    public ProductCollectionItem? AddToCollection(Guid collectionId)
    {
        if (_collections.Any(pc => pc.CollectionId == collectionId))
        {
            return null;
        }

        var item = ProductCollectionItem.Create(Id, collectionId);
        _collections.Add(item);
        return item;
    }

    /// <summary>
    /// Removes this product from a single collection, if it is currently a member.
    /// </summary>
    /// <returns>The removed <see cref="ProductCollectionItem"/>, or <c>null</c> if not a member.</returns>
    public ProductCollectionItem? RemoveFromCollection(Guid collectionId)
    {
        var item = _collections.Find(pc => pc.CollectionId == collectionId);
        if (item is null)
        {
            return null;
        }

        _collections.Remove(item);
        return item;
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
