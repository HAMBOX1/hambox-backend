namespace HAMBOX.Modules.Catalog.Domain.Enums;

/// <summary>
/// Supported sort orders for product listings.
/// </summary>
public enum ProductSortBy
{
    Newest = 0,
    PriceAsc = 1,
    PriceDesc = 2,
    NameAsc = 3,
    NameDesc = 4,
    CategoryAsc = 5,
    CategoryDesc = 6,
    StatusAsc = 7,
    StatusDesc = 8,
    StockAsc = 9,
    StockDesc = 10,
}
