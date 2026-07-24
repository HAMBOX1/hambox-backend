using HAMBOX.Domain.Entities;

namespace HAMBOX.Modules.Catalog.Domain.Instructions;

/// <summary>
/// Represents the private, post-purchase setup/usage documentation for a product.
/// </summary>
/// <remarks>
/// One <see cref="ProductInstructions"/> row per <see cref="Products.Product"/> (enforced by a unique
/// index on <see cref="ProductId"/>). Content is only ever surfaced to customers who have completed a
/// purchase of the product, and only while <see cref="IsPublished"/> is <c>true</c> — see the
/// customer-facing library endpoints for the access check.
/// </remarks>
public sealed class ProductInstructions : AggregateRoot, IAuditable
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ProductInstructions"/> class.
    /// </summary>
    /// <remarks>Required by EF Core.</remarks>
    private ProductInstructions()
    {
    }

    private ProductInstructions(Guid id, Guid productId, string title, string contentHtml)
        : base(id)
    {
        ProductId = productId;
        Title = title;
        ContentHtml = contentHtml;
        Version = 0;
        IsPublished = false;
    }

    /// <summary>
    /// Gets the identifier of the product this documentation belongs to.
    /// </summary>
    public Guid ProductId { get; private set; }

    /// <summary>
    /// Gets the documentation title.
    /// </summary>
    public string Title { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the rich HTML content authored in the admin editor.
    /// </summary>
    public string ContentHtml { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the published version number. Starts at 0 (never published) and increments by one on
    /// every <see cref="Publish"/> call.
    /// </summary>
    public int Version { get; private set; }

    /// <summary>
    /// Gets whether this documentation is currently visible to customers who own the product.
    /// </summary>
    public bool IsPublished { get; private set; }

    /// <inheritdoc />
    public string? CreatedBy { get; private set; }

    /// <inheritdoc />
    public string? ModifiedBy { get; private set; }

    /// <summary>
    /// Creates a new, unpublished documentation draft for a product.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when the product identifier is empty or the title is blank.</exception>
    public static ProductInstructions CreateDraft(Guid productId, string title, string contentHtml)
    {
        if (productId == Guid.Empty)
        {
            throw new ArgumentException("Product identifier must not be empty.", nameof(productId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(title);

        return new ProductInstructions(Guid.NewGuid(), productId, title, contentHtml ?? string.Empty);
    }

    /// <summary>
    /// Saves edits to the documentation content without changing its publish state or version.
    /// </summary>
    /// <remarks>Used for both the admin's manual Save action and editor autosave.</remarks>
    /// <exception cref="ArgumentException">Thrown when the title is blank.</exception>
    public void SaveDraft(string title, string contentHtml)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);

        Title = title;
        ContentHtml = contentHtml ?? string.Empty;
    }

    /// <summary>
    /// Publishes the current content, making it accessible to customers who own the product.
    /// </summary>
    public void Publish()
    {
        IsPublished = true;
        Version++;
    }

    /// <summary>
    /// Unpublishes the documentation, immediately revoking customer access.
    /// </summary>
    public void Unpublish()
    {
        IsPublished = false;
    }
}
