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
    /// Gets the error for when a product update loses an optimistic-concurrency race
    /// (its row version no longer matches what was loaded — someone else saved first).
    /// </summary>
    public static readonly Error ProductConcurrencyConflict = new(
        "Products.ConcurrencyConflict",
        "This product was just changed by someone else. Refresh and try again.");

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
    /// Gets the error for when a product has no active, visible variant backed by real
    /// inventory, so it cannot be sold — there is no way to produce a genuine deliverable.
    /// </summary>
    public static readonly Error ProductNotPurchasable = new(
        "Products.NotPurchasable",
        "This product is not available for purchase yet.");

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

    /// <summary>
    /// Gets the error for when inventory (a batch or codes) is created against a variant that
    /// is not Active — e.g. Draft, Inactive, or Archived. Archived variants must never receive
    /// new stock, since they are meant to be wound down, not restocked.
    /// </summary>
    public static readonly Error VariantNotActive = new(
        "Inventory.VariantNotActive",
        "The variant is not active and cannot receive new inventory.");

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

    /// <summary>
    /// Gets the error for when permanent variant deletion is blocked because usage inspection
    /// (re-run fresh, inside the same transaction as the delete attempt — never trusting counts
    /// the frontend already saw) found either protected history or removable data that has not
    /// been cleaned up yet. The frontend should reopen the usage-inspection dialog rather than
    /// retry the delete.
    /// </summary>
    public static readonly Error VariantHasProtectedUsage = new(
        "Inventory.VariantHasProtectedUsage",
        "This variant is still in use and cannot be permanently deleted. Clean up removable data or archive it instead.");

    public static readonly Error CategoryHasChildren = new(
        "Categories.HasChildren",
        "The category has child categories and cannot be deleted.");

    public static readonly Error CategoryCycle = new(
        "Categories.Cycle",
        "The parent assignment would create a category cycle.");

    /// <summary>
    /// Gets the error for when an uploaded category image is invalid.
    /// </summary>
    public static readonly Error InvalidCategoryImage = new(
        "Categories.InvalidImage",
        "The uploaded image is invalid.");

    /// <summary>
    /// Gets the error for when a category has no image to remove.
    /// </summary>
    public static readonly Error CategoryImageNotFound = new(
        "Categories.ImageNotFound",
        "The category does not have an image to remove.");

    public static readonly Error CollectionNotFound = new(
        "Collections.NotFound",
        "The collection with the specified identifier was not found.");

    public static readonly Error CollectionHasChildren = new(
        "Collections.HasChildren",
        "The collection has child collections and cannot be deleted.");

    public static readonly Error CollectionCycle = new(
        "Collections.Cycle",
        "The parent assignment would create a collection cycle.");

    public static readonly Error CollectionIsSystem = new(
        "Collections.IsSystem",
        "System-defined collections cannot be deleted.");

    public static readonly Error PackageJobNotFound = new(
        "ImportExport.PackageJobNotFound",
        "The import/export job was not found.");

    public static readonly Error PackageNotUploaded = new(
        "ImportExport.PackageNotUploaded",
        "Upload a package before validating or executing an import.");

    public static readonly Error PackageAlreadyExecuted = new(
        "ImportExport.PackageAlreadyExecuted",
        "This import has already been executed.");

    public static readonly Error UnsupportedPackageFormat = new(
        "ImportExport.UnsupportedFormat",
        "Only .hambox, .xlsx, .xlsm, and .csv files are supported.");

    public static readonly Error PackagePasswordRequired = new(
        "ImportExport.PasswordRequired",
        "This package is password-protected. Provide the password to continue.");

    public static readonly Error InvalidPackagePassword = new(
        "ImportExport.InvalidPassword",
        "The package password is incorrect or the file is corrupted.");

    public static readonly Error PackageParsingFailed = new(
        "ImportExport.ParsingFailed",
        "The file could not be read. Check that it matches the expected template.");

    public static readonly Error ExportScopeEmpty = new(
        "ImportExport.ExportScopeEmpty",
        "Select at least one product, a filter, or the entire catalog to export.");

    public static readonly Error InvalidInstructionsImage = new(
        "Instructions.InvalidImage",
        "The uploaded image is invalid.");

    public static readonly Error InstructionsNotFound = new(
        "Instructions.NotFound",
        "Save instructions content before publishing.");
}
