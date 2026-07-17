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
        bool includeImages)
    {
        var images = includeImages ? ToProductImageDtos(product.Images) : null;

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
            GetPrimaryImageUrl(product),
            images,
            product.CreatedOnUtc);
    }
}
