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
/// <param name="CategoryId">The primary category identifier.</param>
/// <param name="CategoryName">The primary category name (English).</param>
/// <param name="CategoryNameAr">The primary category name (Arabic).</param>
/// <param name="AdditionalCategoryIds">The identifiers of additional (non-primary) categories this product is cross-listed under.</param>
/// <param name="CollectionIds">The identifiers of the internal, owner-only collections this product is organized under.</param>
/// <param name="PrimaryImageUrl">The URL of the primary image, if any.</param>
/// <param name="Images">The ordered product images. Populated on detail reads.</param>
/// <param name="CreatedOnUtc">When the product was created.</param>
/// <param name="AvailableStock">Total available inventory code count across all variants. Populated on list reads.</param>
/// <param name="CollectionIds">The identifiers of the internal, owner-only collections this product is organized under.</param>
/// <param name="DisplayImageUrl">
/// The image to render for this product: its own image, else its category's image, else the
/// nearest ancestor category's image, else a placeholder — always non-null. Resolved server-side
/// via <see cref="Services.ProductDisplayImageResolver"/>; the frontend should never re-derive this.
/// </param>
/// <param name="DisplayImageSource">Where <see cref="DisplayImageUrl"/> came from — see <see cref="Services.ProductDisplayImageSource"/>.</param>
/// <param name="PublicReleaseOnUtc">Null if already public; otherwise the date it becomes public to non-members (Early Access membership benefit).</param>
/// <param name="IsMembersOnly">True if the product is restricted to one or more membership plans (Exclusive Products benefit), regardless of the current viewer.</param>
/// <param name="CanPurchase">True if the current viewer may purchase this product right now (both membership-restriction and release-date gates satisfied).</param>
/// <param name="RequiredPlanNames">When <see cref="IsMembersOnly"/> is true, the plan(s) that grant access — used for the upgrade CTA.</param>
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
    IReadOnlyList<Guid> AdditionalCategoryIds,
    string? PrimaryImageUrl,
    IReadOnlyList<ProductImageDto>? Images,
    DateTimeOffset CreatedOnUtc,
    int AvailableStock = 0,
    IReadOnlyList<Guid>? CollectionIds = null,
    string DisplayImageUrl = Services.ProductDisplayImageResolver.PlaceholderImageUrl,
    string DisplayImageSource = Services.ProductDisplayImageSource.Placeholder,
    DateTime? PublicReleaseOnUtc = null,
    bool IsMembersOnly = false,
    bool CanPurchase = true,
    IReadOnlyList<string>? RequiredPlanNames = null);
