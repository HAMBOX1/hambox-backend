namespace HAMBOX.Modules.Commerce.Domain.Enums;

/// <summary>
/// Whether the money for a refunded / cancelled paid order has actually been returned to the customer. HAMBOX
/// marking an order "Refunded" changes our books and releases stock; it does not move money — the payment gateways
/// have no verified automatic-refund path, so the return is made in the gateway / wallet and then recorded here.
/// </summary>
public enum RefundSettlementStatus
{
    /// <summary>No money to return (never paid, development payment, or not refunded).</summary>
    None = 0,

    /// <summary>The order is refunded in HAMBOX but the customer has not been paid back yet.</summary>
    AwaitingGatewayRefund = 1,

    /// <summary>An admin recorded that the money was returned, with the gateway reference.</summary>
    Settled = 2,
}
