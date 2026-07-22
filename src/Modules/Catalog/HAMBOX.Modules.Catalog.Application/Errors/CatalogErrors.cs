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

    public static readonly Error ProductBulkEmpty = new(
        "Products.BulkEmpty",
        "Select at least one product for bulk actions.");

    public static Error ProductBulkActionNotSupported(string action) => new(
        "Products.BulkActionNotSupported",
        $"Bulk action '{action}' is not supported.");

    public static Error ProductBulkSelectionTooLarge(int maxMatches) => new(
        "Products.BulkSelectionTooLarge",
        $"More than {maxMatches} products match this filter. Narrow the filter before running a bulk action.");

    public static readonly Error InvalidPriceAdjustment = new(
        "Products.InvalidPriceAdjustment",
        "The price adjustment would result in a negative price.");

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

    public static readonly Error VariantNotFound = new(
        "Inventory.VariantNotFound",
        "The product variant was not found.");

    public static readonly Error VariantRequired = new(
        "Inventory.VariantRequired",
        "A product variant must be selected for this product.");

    public static readonly Error SupplierNotFound = new(
        "Inventory.SupplierNotFound",
        "The inventory supplier was not found.");

    public static readonly Error BatchNotFound = new(
        "Inventory.BatchNotFound",
        "The inventory batch was not found.");

    public static readonly Error CodeNotFound = new(
        "Inventory.CodeNotFound",
        "The inventory code was not found.");

    public static readonly Error DuplicateCode = new(
        "Inventory.DuplicateCode",
        "The digital code already exists in inventory.");

    public static readonly Error InsufficientInventory = new(
        "Inventory.InsufficientInventory",
        "Insufficient digital codes available for this variant.");

    public static Error InsufficientInventoryQuantity(int available, int requested, int alreadyInCart) =>
        new(
            "Inventory.InsufficientInventory",
            alreadyInCart > 0
                ? $"Only {available} code(s) in stock. Your cart already has {alreadyInCart}; cannot reach quantity {requested}."
                : $"Only {available} code(s) in stock; requested quantity is {requested}.");

    public static readonly Error DuplicateVariantCombination = new(
        "Inventory.DuplicateVariantCombination",
        "A variant with this option combination already exists.");

    public static readonly Error VariantBulkEmpty = new(
        "Inventory.VariantBulkEmpty",
        "Select at least one variant for bulk actions.");

    public static readonly Error OptionGroupNotFound = new(
        "Inventory.OptionGroupNotFound",
        "The option group was not found.");

    public static readonly Error OptionNotFound = new(
        "Inventory.OptionNotFound",
        "The product option was not found.");

    public static readonly Error OptionInUse = new(
        "Inventory.OptionInUse",
        "The option is used by one or more variants and cannot be removed.");

    public static readonly Error OptionGroupInUse = new(
        "Inventory.OptionGroupInUse",
        "The option group is used by one or more variants and cannot be removed.");

    public static readonly Error NoOptionGroups = new(
        "Inventory.NoOptionGroups",
        "Create at least one option group with values before generating variants.");

    public static readonly Error EmptyOptionGroup = new(
        "Inventory.EmptyOptionGroup",
        "Every option group must contain at least one option value.");

    public static readonly Error InvalidCodeStatus = new(
        "Inventory.InvalidCodeStatus",
        "The inventory code status does not allow this operation.");

    public static readonly Error CategoryHasChildren = new(
        "Categories.HasChildren",
        "The category has child categories and cannot be deleted.");

    public static readonly Error CategoryCycle = new(
        "Categories.Cycle",
        "The parent assignment would create a category cycle.");
}
