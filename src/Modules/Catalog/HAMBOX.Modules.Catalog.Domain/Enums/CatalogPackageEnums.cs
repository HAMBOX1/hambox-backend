namespace HAMBOX.Modules.Catalog.Domain.Enums;

public enum CatalogPackageDirection
{
    Export = 0,
    Import = 1,
}

/// <summary>
/// The three supported wire formats. <see cref="Hambox"/> carries the full relational catalog
/// graph in one file; <see cref="Xlsx"/>/<see cref="Csv"/> always describe exactly one
/// <see cref="CatalogImportEntityType"/> at a time (see the Products/Categories/Inventory/Codes
/// templates).
/// </summary>
public enum CatalogPackageFormat
{
    Hambox = 0,
    Xlsx = 1,
    Csv = 2,
}

public enum CatalogPackageJobStatus
{
    Uploaded = 0,
    Queued = 1,
    Processing = 2,
    Completed = 3,
    Failed = 4,
}

/// <summary>
/// Which slice of the catalog an xlsx/csv upload describes. Meaningless for
/// <see cref="CatalogPackageFormat.Hambox"/>, which always carries every entity type at once
/// (<see cref="FullPackage"/>).
/// </summary>
public enum CatalogImportEntityType
{
    FullPackage = 0,
    Products = 1,
    Categories = 2,
    Inventory = 3,
    Codes = 4,
}

public enum CatalogDuplicateStrategy
{
    Skip = 0,
    Update = 1,
    Merge = 2,
    Rename = 3,
}

public enum CatalogImportRowStatus
{
    New = 0,
    Updated = 1,
    Duplicate = 2,
    Invalid = 3,
}
