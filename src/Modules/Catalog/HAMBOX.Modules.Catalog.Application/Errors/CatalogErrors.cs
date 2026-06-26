using HAMBOX.SharedKernel.Errors;

namespace HAMBOX.Modules.Catalog.Application.Errors;

/// <summary>
/// Defines predefined errors for the Catalog module.
/// </summary>
public static class CatalogErrors
{
    /// <summary>
    /// Gets the error for when a category is not found.
    /// </summary>
    public static readonly Error CategoryNotFound = new(
        "Categories.NotFound",
        "The category with the specified identifier was not found.");

    /// <summary>
    /// Gets the error for when a category slug is already in use.
    /// </summary>
    public static readonly Error CategorySlugNotUnique = new(
        "Categories.SlugNotUnique",
        "The provided category slug is already in use.");

    /// <summary>
    /// Gets the error for when a product is not found.
    /// </summary>
    public static readonly Error ProductNotFound = new(
        "Products.NotFound",
        "The product with the specified identifier was not found.");

    /// <summary>
    /// Gets the error for when a product status transition is not allowed.
    /// </summary>
    public static readonly Error InvalidProductStatusTransition = new(
        "Products.InvalidStatusTransition",
        "The requested product status transition is not allowed.");

    /// <summary>
    /// Gets the error for when a product does not have sufficient stock.
    /// </summary>
    public static readonly Error InsufficientStock = new(
        "Products.InsufficientStock",
        "The product does not have sufficient stock for the requested quantity.");

    /// <summary>
    /// Gets the error for when a product is not active.
    /// </summary>
    public static readonly Error ProductNotActive = new(
        "Products.NotActive",
        "The product is not active and cannot be purchased.");

    /// <summary>
    /// Gets the error for when a product image is not found.
    /// </summary>
    public static readonly Error ProductImageNotFound = new(
        "Products.ImageNotFound",
        "The product image with the specified identifier was not found.");

    /// <summary>
    /// Gets the error for when an uploaded image is invalid.
    /// </summary>
    public static readonly Error InvalidProductImage = new(
        "Products.InvalidImage",
        "The uploaded image is invalid.");

    /// <summary>
    /// Gets the error for when a product exceeds the maximum image count.
    /// </summary>
    public static readonly Error ProductImageLimitReached = new(
        "Products.ImageLimitReached",
        "The product has reached the maximum number of images.");
}
