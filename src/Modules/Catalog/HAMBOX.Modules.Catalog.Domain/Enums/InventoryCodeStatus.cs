namespace HAMBOX.Modules.Catalog.Domain.Enums;

public enum InventoryCodeStatus
{
    Available = 0,
    Reserved = 1,
    Sold = 2,
    Expired = 3,
    Disabled = 4,
    Returned = 5,
    Lost = 6,
    Invalid = 7,

    /// <summary>Delivery or redemption of this code was attempted and failed; <c>FailureReason</c> says why. Never sellable again until an admin acts.</summary>
    Failed = 8
}
