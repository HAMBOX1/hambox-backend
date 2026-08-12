namespace HAMBOX.Modules.Catalog.Application.Contracts;

public sealed record ProductPlanDto(Guid Id, Guid ProductId, string Name, string Slug, int SortOrder, string Status);

public sealed record ProductOptionDto(Guid Id, Guid OptionGroupId, string Value, string Label, int SortOrder);

public sealed record ProductOptionGroupDto(
    Guid Id,
    Guid ProductId,
    Guid? ParentOptionId,
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
    string? PerformedByUserId,
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

/// <summary>The only DTO in the module that carries a plaintext inventory code — returned solely by the reveal endpoint.</summary>
public sealed record RevealInventoryCodeDto(string DigitalCode);

/// <summary>
/// One counted reference type within a <see cref="VariantUsageCategoryDto"/>. <paramref name="Type"/>
/// is a stable machine key (e.g. "SoldInventoryCodes") the frontend maps to its own translated
/// label/description — display copy is deliberately not sourced from the API, per the i18n convention.
/// </summary>
public sealed record VariantUsageItemDto(string Type, int Count);

/// <summary>
/// For the SafeToDetach category specifically: "detach" is informational, not an action.
/// InventoryBatch/InventoryAuditLog rows have no FK to ProductVariant (deliberately — see
/// InventoryConfigurations), so archiving or permanently deleting a variant never touches, nulls,
/// or removes them. They simply keep pointing at the variant's (now archived/tombstoned) Id,
/// which remains a valid historical identifier — no migration or detachment operation is needed
/// or performed by cleanup or permanent delete. This category exists purely so the admin UI can
/// explain what those references mean without implying they block or require action.
/// </summary>
public sealed record VariantUsageCategoryDto(IReadOnlyList<VariantUsageItemDto> Items, int TotalCount);

/// <summary>
/// Categorized, real-time usage inspection for a single variant, read fresh from the database on
/// every call — never cached, never fabricated. <see cref="CanPermanentlyDelete"/> is true only when
/// <see cref="ProtectedHistory"/>'s total is zero; it does not account for whether SafeToRemove/
/// SafeToDetach items have been cleaned up yet (permanent delete still re-verifies that server-side).
/// </summary>
public sealed record VariantUsageDto(
    Guid VariantId,
    VariantUsageCategoryDto SafeToRemove,
    VariantUsageCategoryDto SafeToDetach,
    VariantUsageCategoryDto ProtectedHistory,
    bool CanPermanentlyDelete);
