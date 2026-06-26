using HAMBOX.Domain.Entities;

namespace HAMBOX.Modules.Catalog.Domain.Images;

/// <summary>
/// Represents an image associated with a product.
/// </summary>
/// <remarks>
/// ProductImage is a child entity owned by the <see cref="Products.Product"/> aggregate.
/// Only one image per product can be marked as primary at a time.
/// The display order determines the sequence in which images are presented.
/// </remarks>
public sealed class ProductImage : Entity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ProductImage"/> class.
    /// </summary>
    /// <remarks>Required by EF Core.</remarks>
    private ProductImage()
    {
    }

    private ProductImage(
        Guid id,
        Guid productId,
        string url,
        string storageKey,
        string fileName,
        string contentType,
        long fileSizeBytes,
        int displayOrder,
        bool isPrimary)
        : base(id)
    {
        ProductId = productId;
        Url = url;
        StorageKey = storageKey;
        FileName = fileName;
        ContentType = contentType;
        FileSizeBytes = fileSizeBytes;
        DisplayOrder = displayOrder;
        IsPrimary = isPrimary;
    }

    /// <summary>
    /// Gets the identifier of the product this image belongs to.
    /// </summary>
    public Guid ProductId { get; private set; }

    /// <summary>
    /// Gets the URL of the image.
    /// </summary>
    public string Url { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the storage provider key for the image file.
    /// </summary>
    public string StorageKey { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the original file name.
    /// </summary>
    public string FileName { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the MIME content type.
    /// </summary>
    public string ContentType { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the file size in bytes.
    /// </summary>
    public long FileSizeBytes { get; private set; }

    /// <summary>
    /// Gets the display order of the image within the product's image collection.
    /// </summary>
    /// <remarks>
    /// Lower values are displayed first. Must be a non-negative integer.
    /// </remarks>
    public int DisplayOrder { get; private set; }

    /// <summary>
    /// Gets a value indicating whether this image is the primary product image.
    /// </summary>
    public bool IsPrimary { get; private set; }

    /// <summary>
    /// Creates a new product image.
    /// </summary>
    /// <param name="productId">The identifier of the owning product.</param>
    /// <param name="url">The image URL.</param>
    /// <param name="displayOrder">The display order position.</param>
    /// <param name="isPrimary">Whether this is the primary image.</param>
    /// <returns>A new <see cref="ProductImage"/> instance.</returns>
    /// <exception cref="ArgumentException">Thrown when the URL is null or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the display order is negative.</exception>
    /// <exception cref="ArgumentException">Thrown when the product identifier is empty.</exception>
    internal static ProductImage Create(
        Guid productId,
        string url,
        string storageKey,
        string fileName,
        string contentType,
        long fileSizeBytes,
        int displayOrder,
        bool isPrimary)
    {
        if (productId == Guid.Empty)
        {
            throw new ArgumentException("Product identifier must not be empty.", nameof(productId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        ArgumentException.ThrowIfNullOrWhiteSpace(storageKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);
        ArgumentOutOfRangeException.ThrowIfNegative(displayOrder);
        ArgumentOutOfRangeException.ThrowIfNegative(fileSizeBytes);

        return new ProductImage(
            Guid.NewGuid(),
            productId,
            url,
            storageKey,
            fileName,
            contentType,
            fileSizeBytes,
            displayOrder,
            isPrimary);
    }

    /// <summary>
    /// Marks this image as the primary image for the product.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when this image is already the primary image.</exception>
    public void MarkAsPrimary()
    {
        if (IsPrimary)
        {
            throw new InvalidOperationException("This image is already the primary image.");
        }

        IsPrimary = true;
    }

    /// <summary>
    /// Removes the primary designation from this image.
    /// </summary>
    internal void UnmarkAsPrimary()
    {
        IsPrimary = false;
    }

    /// <summary>
    /// Updates the display order of this image.
    /// </summary>
    /// <param name="displayOrder">The new display order position.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the display order is negative.</exception>
    internal void UpdateDisplayOrder(int displayOrder)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(displayOrder);

        DisplayOrder = displayOrder;
    }
}
