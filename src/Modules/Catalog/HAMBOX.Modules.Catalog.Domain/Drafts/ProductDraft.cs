using HAMBOX.Domain.Entities;

namespace HAMBOX.Modules.Catalog.Domain.Drafts;

/// <summary>
/// Represents a temporary product draft before publication.
/// </summary>
/// <remarks>
/// ProductDraft is an aggregate root that holds unpublished product data.
/// Drafts allow content managers to prepare product information before
/// committing it to the catalog as a published <see cref="Products.Product"/>.
/// Once published, the draft should be removed.
/// </remarks>
public sealed class ProductDraft : AggregateRoot, IAuditable
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ProductDraft"/> class.
    /// </summary>
    /// <remarks>Required by EF Core.</remarks>
    private ProductDraft()
    {
    }

    private ProductDraft(
        Guid id,
        string nameAr,
        string nameEn,
        string descriptionAr,
        string descriptionEn,
        decimal? price,
        Guid? categoryId,
        Guid createdByUserId)
        : base(id)
    {
        NameAr = nameAr;
        NameEn = nameEn;
        DescriptionAr = descriptionAr;
        DescriptionEn = descriptionEn;
        Price = price;
        CategoryId = categoryId;
        CreatedByUserId = createdByUserId;
    }

    /// <summary>
    /// Gets the draft product name in Arabic.
    /// </summary>
    public string NameAr { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the draft product name in English.
    /// </summary>
    public string NameEn { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the draft product description in Arabic.
    /// </summary>
    public string DescriptionAr { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the draft product description in English.
    /// </summary>
    public string DescriptionEn { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the draft product price.
    /// </summary>
    /// <remarks>
    /// Nullable because a draft may not have a finalized price.
    /// </remarks>
    public decimal? Price { get; private set; }

    /// <summary>
    /// Gets the identifier of the intended category.
    /// </summary>
    /// <remarks>
    /// Nullable because a draft may not have a category assigned yet.
    /// </remarks>
    public Guid? CategoryId { get; private set; }

    /// <summary>
    /// Gets the identifier of the user who created this draft.
    /// </summary>
    public Guid CreatedByUserId { get; private set; }

    /// <inheritdoc />
    public string? CreatedBy { get; private set; }

    /// <inheritdoc />
    public string? ModifiedBy { get; private set; }

    /// <summary>
    /// Creates a new product draft.
    /// </summary>
    /// <param name="nameAr">The product name in Arabic.</param>
    /// <param name="nameEn">The product name in English.</param>
    /// <param name="descriptionAr">The product description in Arabic.</param>
    /// <param name="descriptionEn">The product description in English.</param>
    /// <param name="price">The optional product price.</param>
    /// <param name="categoryId">The optional category identifier.</param>
    /// <param name="createdByUserId">The identifier of the user creating the draft.</param>
    /// <returns>A new <see cref="ProductDraft"/> instance.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when the name fields are null or whitespace, or when the user identifier is empty.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the price is negative.</exception>
    public static ProductDraft Create(
        string nameAr,
        string nameEn,
        string descriptionAr,
        string descriptionEn,
        decimal? price,
        Guid? categoryId,
        Guid createdByUserId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nameAr);
        ArgumentException.ThrowIfNullOrWhiteSpace(nameEn);

        if (createdByUserId == Guid.Empty)
        {
            throw new ArgumentException("User identifier must not be empty.", nameof(createdByUserId));
        }

        if (price.HasValue)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(price.Value);
        }

        return new ProductDraft(
            Guid.NewGuid(),
            nameAr,
            nameEn,
            descriptionAr ?? string.Empty,
            descriptionEn ?? string.Empty,
            price,
            categoryId,
            createdByUserId);
    }

    /// <summary>
    /// Updates the draft with new product information.
    /// </summary>
    /// <param name="nameAr">The new product name in Arabic.</param>
    /// <param name="nameEn">The new product name in English.</param>
    /// <param name="descriptionAr">The new product description in Arabic.</param>
    /// <param name="descriptionEn">The new product description in English.</param>
    /// <param name="price">The new optional product price.</param>
    /// <param name="categoryId">The new optional category identifier.</param>
    /// <exception cref="ArgumentException">Thrown when name fields are null or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the price is negative.</exception>
    public void Update(
        string nameAr,
        string nameEn,
        string descriptionAr,
        string descriptionEn,
        decimal? price,
        Guid? categoryId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nameAr);
        ArgumentException.ThrowIfNullOrWhiteSpace(nameEn);

        if (price.HasValue)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(price.Value);
        }

        NameAr = nameAr;
        NameEn = nameEn;
        DescriptionAr = descriptionAr ?? string.Empty;
        DescriptionEn = descriptionEn ?? string.Empty;
        Price = price;
        CategoryId = categoryId;
    }
}
