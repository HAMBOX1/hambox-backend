using System.Text.Json;
using HAMBOX.Application.BackgroundJobs;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Services;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Commerce.Infrastructure.BackgroundJobs.Handlers;

/// <summary>
/// Shared logic behind <see cref="RetryOrderFulfillmentJobHandler"/> and <see cref="RetryDeliveryJobHandler"/> —
/// both job types retry fulfillment for the same order, they only differ in the alert/queue semantics
/// that led to them being enqueued.
/// </summary>
internal abstract class OrderRetryJobHandlerBase(
    IBackgroundJobSerializer serializer,
    ICommerceDbContext db,
    OrderFulfillmentService fulfillment) : BackgroundJobHandlerBase<string?>(serializer)
{
    public override async Task HandleAsync(string? payload, IBackgroundJobContext context, CancellationToken cancellationToken)
    {
        var orderId = ResolveOrderId(context, payload);
        if (orderId == Guid.Empty)
        {
            throw new InvalidOperationException("Retry job is missing orderId.");
        }

        var order = await db.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken)
            ?? throw new InvalidOperationException($"Order {orderId} not found.");

        var result = await fulfillment.FulfillMissingAsync(order, cancellationToken);
        if (result.CodesDelivered == 0)
        {
            throw new InvalidOperationException("No codes were delivered on retry.");
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static Guid ResolveOrderId(IBackgroundJobContext context, string? payload)
    {
        if (Guid.TryParse(context.RelatedEntityId, out var orderId))
        {
            return orderId;
        }

        if (!string.IsNullOrWhiteSpace(payload))
        {
            using var doc = JsonDocument.Parse(payload);
            if (doc.RootElement.TryGetProperty("orderId", out var prop) && prop.TryGetGuid(out var payloadId))
            {
                return payloadId;
            }
        }

        return Guid.Empty;
    }
}
