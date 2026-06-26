namespace HAMBOX.Modules.Catalog.Domain.Enums;

/// <summary>
/// Represents the lifecycle status of a product.
/// </summary>
public enum ProductStatus
{
    /// <summary>
    /// The product is in draft state and not yet published.
    /// </summary>
    Draft = 0,

    /// <summary>
    /// The product is active and available for sale.
    /// </summary>
    Active = 1,

    /// <summary>
    /// The product is temporarily inactive and not available for sale.
    /// </summary>
    Inactive = 2,

    /// <summary>
    /// The product is permanently archived and no longer available.
    /// </summary>
    Archived = 3
}
