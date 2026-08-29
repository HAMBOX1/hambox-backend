using System.Text.Json;
using HAMBOX.Application.BackgroundJobs;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Referrals;
using HAMBOX.Modules.Commerce.Application.Services;
using HAMBOX.Modules.Commerce.Domain.Enums;
using HAMBOX.Modules.Commerce.Domain.Operations;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Commerce.Infrastructure.BackgroundJobs.Handlers;

/// <summary>
/// Runs a just-paid order's automated-supplier fulfillment attempt off the checkout HTTP request —
/// the Job/Worker counterpart to what <c>CheckoutCommandHandler</c> used to call inline. Calls the
/// exact same, unmodified <see cref="OrderFulfillmentService.QueueAutomatedSupplierFulfillmentAsync"/>
/// checkout already used; this handler is only a transport change, not a new fulfillment path. Order
/// completion itself is decided entirely by the existing
/// <c>CommerceOrderLicenseKeyDeliverySink.OnDeliveredAsync</c> (invoked synchronously inside the call
/// below whenever a supplier resolves immediately, and again later by the existing sweep's
/// reconciliation for anything left ambiguous) — this handler only detects whether ITS invocation was
/// the one that flipped the order to Completed, so it can fire the referral side effect the same way.
/// </summary>
/// <remarks>
/// Deliberately sends no email on completion — checkout already sent "OrderConfirmation"
/// unconditionally right after payment, regardless of fulfillment status, so sending it again here
/// would duplicate that email. Mirrors <c>OrderRetryJobHandlerBase</c>'s identical choice for the same
/// reason (its manual-retry-completes-later path also fires only the referral hook, no email).
/// </remarks>
internal sealed class ExecuteOrderFulfillmentJobHandler(
    IBackgroundJobSerializer serializer,
    ICommerceDbContext db,
    OrderFulfillmentService fulfillmentService,
    ReferralLifecycleService referralLifecycle) : BackgroundJobHandlerBase<string?>(serializer)
{
    public override string JobType => OperationalJobTypes.ExecuteOrderFulfillment;

    public override async Task HandleAsync(string? payload, IBackgroundJobContext context, CancellationToken cancellationToken)
    {
        var orderId = ResolveOrderId(context, payload);
        if (orderId == Guid.Empty)
        {
            throw new InvalidOperationException("ExecuteOrderFulfillment job is missing orderId.");
        }

        var order = await db.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken)
            ?? throw new InvalidOperationException($"Order {orderId} not found.");

        var wasAlreadyCompleted = order.Status == OrderStatus.Completed;

        // Unmodified — every guarantee (idempotent purchase, ambiguous-outcome-stays-Unknown, cheapest
        // eligible supplier, never touching manual inventory) lives entirely inside this call and the
        // SupplierFulfillmentService/state machine it delegates to. See that method's own remarks for
        // why it must run outside any open SQL transaction — which is exactly where a job handler
        // executes from, so no change was needed there either.
        await fulfillmentService.QueueAutomatedSupplierFulfillmentAsync(order, cancellationToken);

        // The delivery sink (invoked from inside the call above, on the same tracked DbContext/order
        // instance) already completed the order if — and only if — every required unit now has a
        // license key. Detect that here purely to fire the same completion side effects checkout's
        // fast (fully-manual) path already fires, not to duplicate the completion decision itself.
        if (!wasAlreadyCompleted && order.Status == OrderStatus.Completed)
        {
            await referralLifecycle.ProcessOrderCompletedAsync(order, cancellationToken);
        }
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
