namespace HAMBOX.Modules.Catalog.Application.Contracts;

/// <summary>
/// Represents a product in the catalog.
/// </summary>
/// <param name="Id">The unique identifier.</param>
/// <param name="NameAr">The product name in Arabic.</param>
/// <param name="NameEn">The product name in English.</param>
/// <param name="DescriptionAr">The product description in Arabic.</param>
/// <param name="DescriptionEn">The product description in English.</param>
/// <param name="Price">The product price.</param>
/// <param name="Status">The product status.</param>
/// <param name="CategoryId">The category identifier.</param>
/// <param name="CategoryName">The category name (English).</param>
/// <param name="CategoryNameAr">The category name (Arabic).</param>
/// <param name="PrimaryImageUrl">The URL of the primary image, if any.</param>
/// <param name="Images">The ordered product images. Populated on detail reads.</param>
/// <param name="CreatedOnUtc">When the product was created.</param>
public sealed record ProductDto(
    Guid Id,
    string NameAr,
    string NameEn,
    string DescriptionAr,
    string DescriptionEn,
    decimal Price,
    string Status,
    Guid CategoryId,
    string CategoryName,
    string CategoryNameAr,
    string? PrimaryImageUrl,
    IReadOnlyList<ProductImageDto>? Images,
    DateTimeOffset CreatedOnUtc);
