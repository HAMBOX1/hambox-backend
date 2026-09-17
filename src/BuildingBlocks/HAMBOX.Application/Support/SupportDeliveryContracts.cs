namespace HAMBOX.Application.Support;

/// <summary>
/// What Commerce needs to open a delivery conversation for a <c>FulfillmentMode.ChatDelivery</c> order
/// item — deliberately minimal, no attachments/priority/category, since the Support module fills those
/// in with its own sensible defaults for this kind of ticket.
/// </summary>
public sealed record DeliveryTicketRequest(
    string CustomerUserId,
    Guid RelatedOrderId,
    Guid RelatedProductId,
    string Subject,
    string Body);

/// <summary>
/// Lets Commerce open a customer support ticket without depending on the Support module directly —
/// Support.Application already depends on Commerce.Application (for order-context lookups in
/// <c>TicketContextBuilder</c>), so a direct Commerce → Support reference would be circular. This
/// contract lives here instead (same reason as <c>IFulfillmentRouter</c> in
/// <see cref="HAMBOX.Application.Fulfillment"/>) and is implemented by the Support module, registered
/// in DI.
/// </summary>
public interface IDeliveryTicketService
{
    /// <summary>Creates the ticket and returns its id — never throws for a validation-shaped failure;
    /// callers treat this as best-effort and must not let a failure here roll back order fulfillment.</summary>
    Task<Guid> CreateDeliveryTicketAsync(DeliveryTicketRequest request, CancellationToken cancellationToken = default);
}
