using HAMBOX.Application.Support;
using HAMBOX.Modules.Support.Application.Features.Tickets.CreateTicket;
using MediatR;
using Microsoft.Extensions.Logging;

namespace HAMBOX.Modules.Support.Application.Services;

/// <summary>
/// Implements <see cref="IDeliveryTicketService"/> (contract in BuildingBlocks — see that interface's
/// doc comment for why) by sending the same <see cref="CreateTicketCommand"/> the customer
/// self-service and WhatsApp-bot entry points already use — no category/priority override, so it
/// gets whichever defaults <c>CreateTicketCommandHandler</c> resolves for an uncategorized ticket.
/// </summary>
public sealed class DeliveryTicketService(ISender sender, ILogger<DeliveryTicketService> logger)
    : IDeliveryTicketService
{
    public async Task<Guid> CreateDeliveryTicketAsync(DeliveryTicketRequest request, CancellationToken cancellationToken = default)
    {
        var result = await sender.Send(
            new CreateTicketCommand(
                request.CustomerUserId,
                request.CustomerUserId,
                request.Subject,
                request.Body,
                CategoryId: null,
                PriorityId: null,
                request.RelatedOrderId,
                request.RelatedProductId,
                CustomerCountry: null,
                CustomerBrowser: null,
                CustomerDevice: null,
                CustomerIpAddress: null),
            cancellationToken);

        if (!result.IsSuccess)
        {
            // Best-effort per the interface contract — the order is already paid and must not roll
            // back because a ticket couldn't be created. An admin can open one manually from the order.
            logger.LogWarning(
                "Failed to auto-create a delivery ticket for order {OrderId}: {ErrorCode}.",
                request.RelatedOrderId, result.Error.Code);
            return Guid.Empty;
        }

        return result.Value.Id;
    }
}
