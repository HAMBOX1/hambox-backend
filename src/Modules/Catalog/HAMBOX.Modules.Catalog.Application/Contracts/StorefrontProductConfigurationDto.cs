namespace HAMBOX.Modules.Catalog.Application.Contracts;

public sealed record StorefrontProductConfigurationDto(
    Guid ProductId,
    decimal BasePrice,
    IReadOnlyList<ProductOptionGroupDto> OptionGroups,
    IReadOnlyList<StorefrontVariantDto> Variants);

public sealed record StorefrontVariantDto(
    Guid Id,
    string Sku,
    decimal Price,
    decimal? ComparePrice,
    int AvailableStock,
    bool IsLowStock,
    bool IsOutOfStock,
    IReadOnlyList<Guid> OptionIds,
    bool IsCompleteCombination);
