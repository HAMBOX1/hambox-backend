namespace HAMBOX.Modules.Commerce.Domain.Enums;

/// <summary>
/// Represents the lifecycle status of an order.
/// </summary>
public enum OrderStatus
{
    /// <summary>
    /// The order has been placed and is awaiting fulfillment.
    /// </summary>
    Pending = 0,

    /// <summary>
    /// The order has been completed successfully.
    /// </summary>
    Completed = 1,

    /// <summary>
    /// The order has been cancelled.
    /// </summary>
    Cancelled = 2
}
