using HAMBOX.Application.Fulfillment;
using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Catalog.Domain.Enums;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Domain.Account;
using HAMBOX.Modules.Commerce.Domain.Enums;
using HAMBOX.Modules.Commerce.Domain.Orders;
using HAMBOX.Modules.Suppliers.Application.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HAMBOX.Modules.Commerce.Application.Services;

public sealed record OrderFulfillmentResult(int CodesDelivered, bool OrderCompleted);

/// <summary>
/// Outcome of <see cref="OrderFulfillmentService.QueueAutomatedSupplierFulfillmentAsync"/> — counts
/// only, never anything sensitive (no provider names, no delivered codes), safe for admin-facing logs.
/// </summary>
public sealed record AutomatedSupplierFulfillmentSummary(int ShortfallLines, int Queued, int NoSupplierAvailable);

public sealed class OrderFulfillmentService
{
    private readonly ICommerceDbContext _commerceDb;
    private readonly IInventoryEngine _inventoryEngine;
    private readonly ISupplierFulfillmentService _supplierFulfillmentService;
    private readonly IFulfillmentRouter _router;
    private readonly ILogger<OrderFulfillmentService> _logger;

    public OrderFulfillmentService(
        ICommerceDbContext commerceDb,
        IInventoryEngine inventoryEngine,
        ISupplierFulfillmentService supplierFulfillmentService,
        IFulfillmentRouter router,
        ILogger<OrderFulfillmentService> logger)
    {
        _commerceDb = commerceDb;
        _inventoryEngine = inventoryEngine;
        _supplierFulfillmentService = supplierFulfillmentService;
        _router = router;
        _logger = logger;
    }

    public async Task<OrderFulfillmentResult> FulfillMissingAsync(Order order, CancellationToken cancellationToken)
    {
        if (order.Kind == OrderKind.Membership)
        {
            return new OrderFulfillmentResult(0, false);
        }

        if (order.PaymentStatus != PaymentStatus.Paid)
        {
            throw new InvalidOperationException("Only paid orders can be fulfilled.");
        }

        if (order.Status is OrderStatus.Cancelled or OrderStatus.Refunded or OrderStatus.Failed)
        {
            throw new InvalidOperationException("Cancelled, refunded, or failed orders cannot be fulfilled.");
        }

        var existingKeys = await _commerceDb.OrderLicenseKeys
            .Where(k => k.OrderId == order.Id)
            .ToListAsync(cancellationToken);

        var keysByItem = existingKeys.GroupBy(k => k.OrderItemId).ToDictionary(g => g.Key, g => g.Count());
        var delivered = 0;

        await _inventoryEngine.ExpireStaleReservationsAsync(cancellationToken);

        foreach (var item in order.Items.Where(i => i.LineItemType == OrderLineItemType.Product && i.ProductId is Guid))
        {
            keysByItem.TryGetValue(item.Id, out var existingCount);
            var missing = item.Quantity - existingCount;
            if (missing <= 0)
            {
                continue;
            }

            if (item.ProductVariantId is Guid variantId)
            {
                var readiness = await _router.GetReadinessAsync(variantId, cancellationToken);
                if (!readiness.ManualAllowed)
                {
                    // SupplierOnly/SupplierFirst — manual inventory must never be touched here, even if
                    // stock genuinely exists. The supplier step (QueueAutomatedSupplierFulfillmentAsync,
                    // or SupplierFirst's terminal-fallback in CommerceOrderLicenseKeyDeliverySink) is the
                    // only path allowed to cover this item's shortfall.
                    _logger.LogInformation(
                        "FulfillmentRouting: skipping manual reservation for order {OrderId} item {OrderItemId} — mode {Mode} does not allow manual inventory.",
                        order.Id, item.Id, readiness.Mode);
                    continue;
                }

                // Partial, never-throwing reservation — takes whatever manual stock actually exists (0
                // up to `missing`) instead of the all-or-nothing ReserveCodesAsync. This is what makes
                // ManualFirst's "consume available manual inventory first, let the supplier cover the
                // rest" semantics possible; it also makes ManualOnly/admin-retry correctly deliver a
                // partial quantity for one order line instead of one short line throwing and blocking
                // every other line in the same order from being processed at all.
                var reserved = await _inventoryEngine.ReservePartialCodesAsync(
                    variantId,
                    missing,
                    order.UserId,
                    cartId: null,
                    cancellationToken);

                _logger.LogInformation(
                    "FulfillmentRouting: order {OrderId} item {OrderItemId} mode {Mode} — manual reserved {ManualQuantity} of {RequestedQuantity}.",
                    order.Id, item.Id, readiness.Mode, reserved.Count, missing);

                if (reserved.Count == 0)
                {
                    continue;
                }

                var assignments = reserved
                    .Select((code, index) => (item.Id, code.CodeId))
                    .ToList();

                var committed = await _inventoryEngine.CommitReservationsAsync(
                    order.Id,
                    assignments,
                    cancellationToken);

                foreach (var code in committed)
                {
                    _commerceDb.OrderLicenseKeys.Add(OrderLicenseKey.Create(
                        order.Id,
                        item.Id,
                        item.ProductId!.Value,
                        code.DigitalCode,
                        item.ProductVariantId,
                        code.CodeId));
                    delivered++;
                }
            }

            // Legacy order lines with no ProductVariantId predate the fix that requires a real,
            // inventory-backed variant at checkout. There is no genuine deliverable to auto-assign
            // here — skip rather than fabricate a license key; an admin must resolve these via
            // AssignManualCodeAsync with a real code.
        }

        var orderCompleted = false;
        if (delivered > 0 && order.Status is OrderStatus.Pending or OrderStatus.Processing)
        {
            var allKeys = await _commerceDb.OrderLicenseKeys
                .Where(k => k.OrderId == order.Id)
                .ToListAsync(cancellationToken);

            var required = order.Items
                .Where(i => i.LineItemType == OrderLineItemType.Product)
                .Sum(i => i.Quantity);

            if (allKeys.Count >= required && required > 0)
            {
                if (order.Status == OrderStatus.Processing || order.Status == OrderStatus.Pending)
                {
                    order.Complete();
                    orderCompleted = true;
                }
            }
            else if (order.Status == OrderStatus.Pending)
            {
                order.MarkProcessing();
            }
        }

        return new OrderFulfillmentResult(delivered, orderCompleted);
    }

    public async Task<OrderLicenseKey> AssignManualCodeAsync(
        Order order,
        Guid orderItemId,
        string licenseKey,
        CancellationToken cancellationToken)
    {
        if (order.PaymentStatus != PaymentStatus.Paid)
        {
            throw new InvalidOperationException("Manual codes can only be assigned to paid orders.");
        }

        var item = order.Items.FirstOrDefault(i => i.Id == orderItemId)
            ?? throw new InvalidOperationException("Order item was not found.");

        if (item.LineItemType != OrderLineItemType.Product || item.ProductId is not Guid productId)
        {
            throw new InvalidOperationException("Manual codes can only be assigned to product line items.");
        }

        var existingCount = await _commerceDb.OrderLicenseKeys
            .CountAsync(k => k.OrderItemId == orderItemId, cancellationToken);

        if (existingCount >= item.Quantity)
        {
            throw new InvalidOperationException("This line item already has the required number of codes.");
        }

        var key = OrderLicenseKey.Create(
            order.Id,
            orderItemId,
            productId,
            licenseKey.Trim(),
            item.ProductVariantId);

        _commerceDb.OrderLicenseKeys.Add(key);
        return key;
    }

    /// <summary>
    /// The automated-supplier counterpart to <see cref="FulfillMissingAsync"/> — deliberately a
    /// SEPARATE method rather than folded into it, because it must run strictly AFTER the caller's own
    /// DB transaction has committed (see remarks). Re-reads the order's current
    /// <c>OrderLicenseKey</c> state fresh (independent of whatever produced it — checkout's inline
    /// path or <see cref="FulfillMissingAsync"/>'s), and for any product line still short, resolves an
    /// enabled automated <c>Supplier</c> with an active mapping to that product and asks
    /// <see cref="ISupplierFulfillmentService"/> to claim and submit the shortfall. Never touches
    /// manual/<see cref="IInventoryEngine"/> logic at all — manual stock is always attempted first, by
    /// the caller, before this runs; this only ever sees what manual stock could not cover.
    /// </summary>
    /// <remarks>
    /// PAYMENT/TRANSACTION SAFETY: this method must only ever be called after the caller's payment
    /// transaction (<c>ICommerceTransactionService.ExecuteAsync</c>) has already committed — never from
    /// inside it. <see cref="ISupplierFulfillmentService"/> writes to a completely separate DbContext/
    /// connection (the <c>suppliers</c> schema) that <c>ICommerceTransactionService</c> does not share;
    /// calling this from inside the Commerce+Catalog transaction would let a
    /// <c>SupplierFulfillment</c> row commit independently and then survive a later rollback of the
    /// order itself, orphaning a fulfillment for an order that was never actually paid. It also makes
    /// an external HTTP call (via <see cref="ISupplierFulfillmentService.ProcessAsync"/>), which must
    /// never happen inside any open SQL transaction regardless. This method also re-checks
    /// <c>PaymentStatus.Paid</c> itself as a second, independent guard — it is not solely relying on
    /// callers to have checked first.
    /// </remarks>
    public async Task<AutomatedSupplierFulfillmentSummary> QueueAutomatedSupplierFulfillmentAsync(
        Order order, CancellationToken cancellationToken)
    {
        if (order.Kind == OrderKind.Membership || order.PaymentStatus != PaymentStatus.Paid)
        {
            return new AutomatedSupplierFulfillmentSummary(0, 0, 0);
        }

        var existingKeyCounts = await _commerceDb.OrderLicenseKeys
            .Where(k => k.OrderId == order.Id)
            .GroupBy(k => k.OrderItemId)
            .Select(g => new { OrderItemId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.OrderItemId, x => x.Count, cancellationToken);

        var shortfallLines = 0;
        var queued = 0;
        var noSupplierAvailable = 0;

        foreach (var item in order.Items.Where(i => i.LineItemType == OrderLineItemType.Product && i.ProductId is Guid))
        {
            existingKeyCounts.TryGetValue(item.Id, out var existing);
            var shortfall = item.Quantity - existing;
            if (shortfall <= 0)
            {
                continue;
            }

            if (item.ProductVariantId is not Guid variantId)
            {
                continue;
            }

            var readiness = await _router.GetReadinessAsync(variantId, cancellationToken);
            if (readiness.Mode == FulfillmentMode.ManualOnly)
            {
                // Never even resolves a supplier candidate for a ManualOnly variant — this shortfall
                // is expected to stay uncovered by automation; an admin resolves it manually.
                continue;
            }

            shortfallLines++;

            var candidate = readiness.SupplierCandidate;
            if (candidate is null)
            {
                _logger.LogInformation(
                    "FulfillmentRouting: order {OrderId} item {OrderItemId} mode {Mode} — no READY automated supplier candidate, shortfall {Shortfall} left uncovered.",
                    order.Id, item.Id, readiness.Mode, shortfall);
                noSupplierAvailable++;
                continue;
            }

            _logger.LogInformation(
                "FulfillmentRouting: order {OrderId} item {OrderItemId} mode {Mode} — routing {Shortfall} unit(s) to supplier {SupplierId}.",
                order.Id, item.Id, readiness.Mode, shortfall, candidate.SupplierId);

            try
            {
                var requestResult = await _supplierFulfillmentService.RequestFulfillmentAsync(
                    new SupplierFulfillmentRequest(order.Id, item.Id, candidate.SupplierId, candidate.SupplierProductMappingId, shortfall),
                    cancellationToken);

                if (requestResult.IsFailure)
                {
                    _logger.LogWarning(
                        "Automated supplier fulfillment request was rejected for order {OrderId} item {OrderItemId}: {ErrorCode}.",
                        order.Id, item.Id, requestResult.Error.Code);
                    continue;
                }

                queued++;

                // Best-effort immediate submission — if this fails or times out ambiguously, the
                // fulfillment stays Pending/Unknown and the existing sweep (SupplierFulfillmentSweepJobHandler)
                // picks it up; nothing here is load-bearing for correctness, only for speed. Delivered
                // codes (whether resolved here synchronously or later by the sweep's reconciliation)
                // are attached to OrderLicenseKey by CommerceOrderLicenseKeyDeliverySink, invoked by
                // SupplierFulfillmentService itself — never here — so exactly one code path attaches
                // codes regardless of which caller/timing actually resolved the purchase.
                await _supplierFulfillmentService.ProcessAsync(requestResult.Value.FulfillmentId, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Automated supplier fulfillment attempt failed for order {OrderId} item {OrderItemId} — the background sweep will retry.",
                    order.Id, item.Id);
            }
        }

        return new AutomatedSupplierFulfillmentSummary(shortfallLines, queued, noSupplierAvailable);
    }
}
