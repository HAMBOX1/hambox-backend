using HAMBOX.Domain.Entities;
using HAMBOX.Modules.Catalog.Domain.Events;

namespace HAMBOX.Modules.Catalog.Domain.Categories;

/// <summary>
/// Represents a product category in the catalog.
/// </summary>
/// <remarks>
/// Category is an aggregate root that organizes products into logical groups.
/// Each category has bilingual names (Arabic and English) and a unique slug
/// for URL-friendly identification.
/// </remarks>
public sealed class Category : AggregateRoot, IAuditable, ISoftDeletable
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Category"/> class.
    /// </summary>
    /// <remarks>Required by EF Core.</remarks>
    private Category()
    {
    }

    private Category(
        Guid id,
        string nameAr,
        string nameEn,
        string slug)
        : base(id)
    {
        NameAr = nameAr;
        NameEn = nameEn;
        Slug = slug;
        IsActive = true;
    }

    /// <summary>
    /// Gets the category name in Arabic.
    /// </summary>
    public string NameAr { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the category name in English.
    /// </summary>
    public string NameEn { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the URL-friendly slug for the category.
    /// </summary>
    /// <remarks>
    /// The slug must be unique across all categories and is used for
    /// SEO-friendly URLs and programmatic lookups.
    /// </remarks>
    public string Slug { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the optional parent category identifier.
    /// </summary>
    public Guid? ParentId { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the category is active.
    /// </summary>
    public bool IsActive { get; private set; }

    /// <inheritdoc />
    public string? CreatedBy { get; private set; }

    /// <inheritdoc />
    public string? ModifiedBy { get; private set; }

    /// <inheritdoc />
    public bool IsDeleted { get; private set; }

    /// <inheritdoc />
    public DateTimeOffset? DeletedOnUtc { get; private set; }

    /// <summary>
    /// Creates a new active category.
    /// </summary>
    /// <param name="nameAr">The category name in Arabic.</param>
    /// <param name="nameEn">The category name in English.</param>
    /// <param name="slug">The URL-friendly slug.</param>
    /// <returns>A new <see cref="Category"/> instance.</returns>
    /// <exception cref="ArgumentException">Thrown when any required parameter is null or whitespace.</exception>
    public static Category Create(
        string nameAr,
        string nameEn,
        string slug,
        Guid? parentId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nameAr);
        ArgumentException.ThrowIfNullOrWhiteSpace(nameEn);
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);

        var category = new Category(Guid.NewGuid(), nameAr, nameEn, slug);
        category.SetParent(parentId);
        return category;
    }

    /// <summary>
    /// Restores a soft-deleted category.
    /// </summary>
    public void Restore()
    {
        if (!IsDeleted)
        {
            throw new InvalidOperationException("Category is not deleted.");
        }

        IsDeleted = false;
        DeletedOnUtc = null;
    }

    /// <summary>
    /// Updates the category details.
    /// </summary>
    /// <param name="nameAr">The new category name in Arabic.</param>
    /// <param name="nameEn">The new category name in English.</param>
    /// <param name="slug">The new URL-friendly slug.</param>
    /// <exception cref="ArgumentException">Thrown when any required parameter is null or whitespace.</exception>
    public void Update(
        string nameAr,
        string nameEn,
        string slug,
        Guid? parentId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nameAr);
        ArgumentException.ThrowIfNullOrWhiteSpace(nameEn);
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);

        NameAr = nameAr;
        NameEn = nameEn;
        Slug = slug;
        SetParent(parentId);
    }

    /// <summary>
    /// Sets the parent category.
    /// </summary>
    public void SetParent(Guid? parentId)
    {
        if (parentId == Id)
        {
            throw new InvalidOperationException("A category cannot be its own parent.");
        }

        ParentId = parentId;
    }

    /// <summary>
    /// Activates the category, making it visible in the catalog.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the category is already active.</exception>
    public void Activate()
    {
        if (IsActive)
        {
            throw new InvalidOperationException("Category is already active.");
        }

        IsActive = true;

        RaiseDomainEvent(new CategoryActivatedDomainEvent(Id));
    }

    /// <summary>
    /// Deactivates the category, hiding it from the catalog.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the category is already inactive.</exception>
    public void Deactivate()
    {
        if (!IsActive)
        {
            throw new InvalidOperationException("Category is already inactive.");
        }

        IsActive = false;

        RaiseDomainEvent(new CategoryDeactivatedDomainEvent(Id));
    }
}
