namespace HAMBOX.Modules.Catalog.Domain.Enums;

public enum InventoryAuditAction
{
    VariantCreated = 0,
    VariantUpdated = 1,
    VariantDeleted = 2,
    CodeAdded = 3,
    CodeImported = 4,
    CodeExported = 5,
    CodeReserved = 6,
    CodeReleased = 7,
    CodeSold = 8,
    BatchCreated = 9,
    BatchImported = 10,
    SupplierAdded = 11,
    InventoryAdjusted = 12,
    OptionGroupCreated = 13,
    OptionCreated = 14,
    PlanCreated = 15
}
