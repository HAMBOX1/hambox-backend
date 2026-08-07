using HAMBOX.Modules.Catalog.Application.Contracts;
using HAMBOX.Modules.Catalog.Domain.Images;
using HAMBOX.Modules.Catalog.Domain.Products;

namespace HAMBOX.Modules.Catalog.Application.Services;

/// <summary>
/// Maps catalog domain entities to DTOs.
/// </summary>
internal static class CatalogMapper
{
    public const int MaxImagesPerProduct = 10;

    public static ProductImageDto ToProductImageDto(ProductImage image) =>
        new(
            image.Id,
            image.Url,
            image.FileName,
            image.ContentType,
            image.FileSizeBytes,
            image.DisplayOrder,
            image.IsPrimary);

    public static IReadOnlyList<ProductImageDto> ToProductImageDtos(IEnumerable<ProductImage> images) =>
        images
            .OrderBy(image => image.DisplayOrder)
            .ThenBy(image => image.CreatedOnUtc)
            .Select(ToProductImageDto)
            .ToList();

    public static string? GetPrimaryImageUrl(Product product) =>
        product.Images.FirstOrDefault(image => image.IsPrimary)?.Url
        ?? product.Images.OrderBy(image => image.DisplayOrder).FirstOrDefault()?.Url;

    public static ProductDto ToProductDto(
        Product product,
        string categoryName,
        string categoryNameAr,
        bool includeImages,
        string? categoryOwnImageUrl = null,
        string? categoryEffectiveImageUrl = null,
        bool isMembersOnly = false,
        bool canPurchase = true,
        IReadOnlyList<string>? requiredPlanNames = null)
    {
        var images = includeImages ? ToProductImageDtos(product.Images) : null;
        var primaryImageUrl = GetPrimaryImageUrl(product);
        var displayImage = ProductDisplayImageResolver.Resolve(primaryImageUrl, categoryOwnImageUrl, categoryEffectiveImageUrl);

        return new ProductDto(
            product.Id,
            product.NameAr,
            product.NameEn,
            product.DescriptionAr,
            product.DescriptionEn,
            product.Price,
            product.Status.ToString(),
            product.CategoryId,
            categoryName,
            categoryNameAr,
            product.AdditionalCategories.Select(pc => pc.CategoryId).ToList(),
            primaryImageUrl,
            images,
            product.CreatedOnUtc,
            CollectionIds: product.Collections.Select(pc => pc.CollectionId).ToList(),
            DisplayImageUrl: displayImage.Url,
            DisplayImageSource: displayImage.Source,
            PublicReleaseOnUtc: product.PublicReleaseOnUtc,
            IsMembersOnly: isMembersOnly,
            CanPurchase: canPurchase,
            RequiredPlanNames: requiredPlanNames);
    }
}
