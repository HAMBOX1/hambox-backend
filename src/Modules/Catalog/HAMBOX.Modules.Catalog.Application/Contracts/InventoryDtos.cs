namespace HAMBOX.Modules.Catalog.Application.Contracts;

public sealed record ProductPlanDto(Guid Id, Guid ProductId, string Name, string Slug, int SortOrder, string Status);

public sealed record ProductOptionDto(Guid Id, Guid OptionGroupId, string Value, string Label, int SortOrder);

public sealed record ProductOptionGroupDto(
    Guid Id,
    Guid ProductId,
    string Key,
    string DisplayName,
    int SortOrder,
    bool IsRequired,
    IReadOnlyList<ProductOptionDto> Options);

public sealed record ProductVariantDto(
    Guid Id,
    Guid ProductId,
    Guid? PlanId,
    string Sku,
    decimal? PriceOverride,
    decimal? ComparePrice,
    int SortOrder,
    string Status,
    bool IsVisible,
    Guid? MembershipPlanId,
    int LowStockThreshold,
    int AvailableStock,
    int ReservedStock,
    int SoldStock,
    int TotalCodesCount,
    bool IsLowStock,
    bool IsOutOfStock,
    IReadOnlyList<Guid> OptionIds);

public sealed record GenerateProductVariantsResultDto(
    int CreatedCount,
    int PreservedCount,
    int TotalCombinations);

public sealed record InventorySupplierDto(
    Guid Id,
    string CompanyName,
    string? ContactPerson,
    string? Email,
    string? Phone,
    string? Website,
    string? Country,
    string Currency,
    string? Notes,
    string Status);

public sealed record InventoryBatchDto(
    Guid Id,
    Guid VariantId,
    Guid? SupplierId,
    string Name,
    DateTimeOffset? PurchaseDate,
    DateTimeOffset ImportDate,
    string Currency,
    decimal PurchaseCost,
    decimal? ExpectedMargin,
    string? Notes,
    int TotalCodes,
    int AvailableCodes,
    int ReservedCodes,
    int SoldCodes,
    int ReturnedCodes,
    int ExpiredCodes);

public sealed record DigitalInventoryCodeDto(
    Guid Id,
    Guid VariantId,
    Guid BatchId,
    Guid? SupplierId,
    string DigitalCode,
    string? SerialNumber,
    string Status,
    decimal? PurchaseCost,
    decimal? SellingPriceOverride,
    string Currency,
    DateTimeOffset? ExpirationDate,
    DateTimeOffset? ReservedOnUtc,
    DateTimeOffset? SoldOnUtc);

public sealed record InventoryAuditLogDto(
    Guid Id,
    string Action,
    Guid? ProductId,
    Guid? VariantId,
    Guid? BatchId,
    Guid? CodeId,
    string? Details,
    DateTimeOffset OccurredOnUtc);

public sealed record InventoryStatisticsDto(
    int Available,
    int Reserved,
    int Sold,
    int Expired,
    int LowStockVariants,
    int OutOfStockVariants,
    decimal InventoryValue,
    decimal PurchaseCost,
    decimal EstimatedRevenue,
    decimal EstimatedProfit);

public sealed record InventoryReservationDto(
    Guid Id,
    Guid CodeId,
    Guid VariantId,
    string? UserId,
    Guid? CartId,
    DateTimeOffset ExpiresOnUtc,
    bool IsActive);
