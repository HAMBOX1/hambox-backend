using HAMBOX.Application.Fulfillment;
using HAMBOX.Application.Support;
using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Catalog.Domain.Enums;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Domain.Account;
using HAMBOX.Modules.Commerce.Domain.Enums;
using HAMBOX.Modules.Commerce.Domain.Orders;
using HAMBOX.Modules.Suppliers.Application.Abstractions;
using HAMBOX.Modules.Suppliers.Application.Contracts;
using HAMBOX.Modules.Suppliers.Domain.Fulfillments;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HAMBOX.Modules.Commerce.Application.Services;

/// <param name="PendingChatDeliveryTickets">
/// <c>ChatDelivery</c> items whose capacity was just consumed by this call and still need their
/// delivery ticket opened. Deliberately not created inside <see cref="OrderFulfillmentService.FulfillMissingAsync"/>
/// itself — every caller of that method runs it inside (or may run it inside) a Commerce+Catalog
/// transaction, and a Support-schema ticket write must never share that transaction (same rule
/// <see cref="OrderFulfillmentService.QueueAutomatedSupplierFulfillmentAsync"/>'s remarks describe for
/// automated-supplier calls). Callers must invoke
/// <see cref="OrderFulfillmentService.CreatePendingChatDeliveryTicketsAsync"/> with this list strictly
/// after their transaction has committed.
/// </param>
public sealed record OrderFulfillmentResult(
    int CodesDelivered,
    bool OrderCompleted,
    IReadOnlyList<ChatDeliveryPendingTicket> PendingChatDeliveryTickets);

/// <summary>One <c>ChatDelivery</c> unit-quantity fulfilled without its ticket created yet.</summary>
public sealed record ChatDeliveryPendingTicket(Guid OrderItemId, Guid ProductId, int Quantity);

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
    private readonly ISupplierPricingEngine _pricingEngine;
    private readonly ISuppliersDbContext _suppliersDb;
    private readonly IDeliveryTicketService _deliveryTicketService;
    private readonly ILogger<OrderFulfillmentService> _logger;

    public OrderFulfillmentService(
        ICommerceDbContext commerceDb,
        IInventoryEngine inventoryEngine,
        ISupplierFulfillmentService supplierFulfillmentService,
        IFulfillmentRouter router,
        ISupplierPricingEngine pricingEngine,
        ISuppliersDbContext suppliersDb,
        IDeliveryTicketService deliveryTicketService,
        ILogger<OrderFulfillmentService> logger)
    {
        _commerceDb = commerceDb;
        _inventoryEngine = inventoryEngine;
        _supplierFulfillmentService = supplierFulfillmentService;
        _router = router;
        _pricingEngine = pricingEngine;
        _suppliersDb = suppliersDb;
        _deliveryTicketService = deliveryTicketService;
        _logger = logger;
    }

    public async Task<OrderFulfillmentResult> FulfillMissingAsync(Order order, CancellationToken cancellationToken)
    {
        if (order.Kind == OrderKind.Membership)
        {
            return new OrderFulfillmentResult(0, false, []);
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
        var pendingChatDeliveryTickets = new List<ChatDeliveryPendingTicket>();

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

                if (readiness.Mode == FulfillmentMode.ChatDelivery)
                {
                    var chatDelivered = await FulfillViaChatDeliveryAsync(order, item, variantId, missing, cancellationToken);
                    delivered += chatDelivered;
                    if (chatDelivered > 0)
                    {
                        pendingChatDeliveryTickets.Add(new ChatDeliveryPendingTicket(item.Id, item.ProductId!.Value, chatDelivered));
                    }

                    continue;
                }

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

        return new OrderFulfillmentResult(delivered, orderCompleted, pendingChatDeliveryTickets);
    }

    /// <summary>
    /// Fulfills a <see cref="FulfillmentMode.ChatDelivery"/> item: consumes the variant's remaining
    /// <c>ManualDeliveryCapacity</c> (all-or-nothing for the whole shortfall — checkout already
    /// verified enough capacity existed for the full cart line, so a partial consume here would only
    /// mean a race with another order, in which case leaving this item untouched for a later retry is
    /// correct) and stages the sentinel license-key row so completion counts it. Deliberately does NOT
    /// create the delivery ticket here — see <see cref="ChatDeliveryPendingTicket"/>'s doc comment for
    /// why that must happen strictly after the caller's transaction commits. Never throws; capacity
    /// exhaustion just leaves the item undelivered for the existing retry-job path to pick up later,
    /// exactly like a manual variant that ran out of codes.
    /// </summary>
    private async Task<int> FulfillViaChatDeliveryAsync(
        Order order,
        OrderItem item,
        Guid variantId,
        int quantity,
        CancellationToken cancellationToken)
    {
        var consumed = await _inventoryEngine.TryConsumeManualDeliveryCapacityAsync(variantId, quantity, cancellationToken);
        if (!consumed)
        {
            _logger.LogInformation(
                "FulfillmentRouting: order {OrderId} item {OrderItemId} mode ChatDelivery — insufficient ManualDeliveryCapacity for {Quantity} unit(s).",
                order.Id, item.Id, quantity);
            return 0;
        }

        _logger.LogInformation(
            "FulfillmentRouting: order {OrderId} item {OrderItemId} mode ChatDelivery — consumed {Quantity} unit(s) of capacity, ticket pending.",
            order.Id, item.Id, quantity);

        for (var i = 0; i < quantity; i++)
        {
            // No real digital code exists for this mode — this sentinel row exists purely so the
            // order-completion count below (which counts OrderLicenseKey rows) treats this unit as
            // delivered exactly like a code-backed one, without duplicating that logic for a second
            // fulfillment kind. The string itself is customer-safe in case it's ever surfaced the same
            // way a real revealed code would be.
            _commerceDb.OrderLicenseKeys.Add(OrderLicenseKey.Create(
                order.Id,
                item.Id,
                item.ProductId!.Value,
                "Delivered via support chat — see your ticket for details.",
                item.ProductVariantId));
        }

        return quantity;
    }

    /// <summary>
    /// Opens the delivery ticket for each pending <c>ChatDelivery</c> item returned by
    /// <see cref="FulfillMissingAsync"/> — call this only after the caller's own transaction (if any)
    /// around that call has committed. Best-effort per item: a failed ticket creation is logged and
    /// skipped rather than throwing, since the order is already paid and its capacity already spent;
    /// an admin can open one manually from the order if this never catches up on retry.
    /// </summary>
    public async Task CreatePendingChatDeliveryTicketsAsync(
        Order order,
        IReadOnlyList<ChatDeliveryPendingTicket> pending,
        CancellationToken cancellationToken)
    {
        foreach (var ticket in pending)
        {
            var ticketId = await _deliveryTicketService.CreateDeliveryTicketAsync(
                new DeliveryTicketRequest(
                    order.UserId,
                    order.Id,
                    ticket.ProductId,
                    Subject: $"Order {order.OrderNumber} — delivery",
                    Body: $"Thanks for your order! Our team will deliver order {order.OrderNumber} to you right here — reply with any details we need to get started."),
                cancellationToken);

            _logger.LogInformation(
                "FulfillmentRouting: order {OrderId} item {OrderItemId} mode ChatDelivery — ticket {TicketId} created for {Quantity} unit(s).",
                order.Id, ticket.OrderItemId, ticketId, ticket.Quantity);
        }
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
            if (readiness.Mode is FulfillmentMode.ManualOnly or FulfillmentMode.ChatDelivery)
            {
                // Never even resolves a supplier candidate for these modes — a ManualOnly shortfall is
                // expected to stay uncovered by automation until an admin resolves it manually, and a
                // ChatDelivery shortfall (capacity exhausted) is covered by topping up capacity and
                // retrying, never by an automated supplier.
                continue;
            }

            shortfallLines++;

            // Cheapest-eligible-supplier selection, ranked by SELLING price (cost + margin) via
            // ISupplierPricingEngine — this guarantees the supplier actually purchased from here is
            // always the same one the customer's displayed price was computed from (see that
            // interface's remarks for why ranking by raw cost alone would not guarantee this when
            // suppliers have different margins). Never any live provider call (still a "fast local
            // decision", inherited from ISupplierRoutingEngine).
            var routing = await _pricingEngine.ResolveAsync(
                new SupplierRoutingRequest(item.ProductId!.Value, variantId, shortfall), cancellationToken);

            if (routing.RankedBySellingPriceAscending.Count == 0)
            {
                _logger.LogInformation(
                    "FulfillmentRouting: order {OrderId} item {OrderItemId} mode {Mode} — no eligible automated supplier candidate, shortfall {Shortfall} left uncovered.",
                    order.Id, item.Id, readiness.Mode, shortfall);
                noSupplierAvailable++;
                await RecordRoutingDecisionAsync(order.Id, item.Id, selected: null, routing, fallbackOccurred: false, cancellationToken);
                continue;
            }

            SupplierPricingCandidate? selectedCandidate = null;
            var fallbackOccurred = false;
            var exhausted = true;
            var anyAttemptQueued = false;

            for (var i = 0; i < routing.RankedBySellingPriceAscending.Count; i++)
            {
                var candidate = routing.RankedBySellingPriceAscending[i];
                var isLastCandidate = i == routing.RankedBySellingPriceAscending.Count - 1;

                _logger.LogInformation(
                    "FulfillmentRouting: order {OrderId} item {OrderItemId} mode {Mode} — attempting {Shortfall} unit(s) via supplier {SupplierId} (attempt {Attempt}/{Total}).",
                    order.Id, item.Id, readiness.Mode, shortfall, candidate.SupplierId, i + 1, routing.RankedBySellingPriceAscending.Count);

                try
                {
                    var requestResult = await _supplierFulfillmentService.RequestFulfillmentAsync(
                        new SupplierFulfillmentRequest(order.Id, item.Id, candidate.SupplierId, candidate.SupplierProductMappingId, shortfall),
                        cancellationToken);

                    if (requestResult.IsFailure)
                    {
                        // Rejected before any external call was ever made (e.g. a race where the mapping
                        // was disabled between routing and this attempt, or another worker currently owns
                        // the claim) — safe to consider the next candidate, per the "definitive failure
                        // before creating the supplier order" rule. ConcurrentClaimLost is the one
                        // exception: another in-flight worker already owns this exact candidate, so this
                        // invocation backs off entirely rather than risking a second worker also failing
                        // over onto a different supplier for the same shortfall.
                        _logger.LogWarning(
                            "Automated supplier fulfillment request was rejected for order {OrderId} item {OrderItemId}, supplier {SupplierId}: {ErrorCode}.",
                            order.Id, item.Id, candidate.SupplierId, requestResult.Error.Code);

                        if (requestResult.Error == HAMBOX.Modules.Suppliers.Application.Errors.SupplierErrors.ConcurrentClaimLost)
                        {
                            exhausted = false; // another worker is legitimately handling this — not "no supplier available"
                            break;
                        }

                        continue;
                    }

                    // Counted once per SHORTFALL LINE (not per failover attempt) — an admin-facing coarse
                    // count of "how many lines got at least one automated attempt", matching this
                    // counter's meaning before failover existed; per-attempt/per-candidate detail lives
                    // in the routing audit log instead (see RecordRoutingDecisionAsync).
                    anyAttemptQueued = true;

                    // Never re-purchase a resumed non-terminal attempt — mirrors the background sweep's
                    // own rule (ProcessDueFulfillmentsAsync only ever calls ProcessAsync for Pending rows,
                    // ReconcileAsync for everything else). A brand-new attempt (Pending) is the only case
                    // safe to submit; Unknown/Submitted/Submitting means a purchase call already happened
                    // for this exact candidate and must only ever be resolved via status lookup.
                    var outcomeResult = requestResult.Value.Status == SupplierFulfillmentStatus.Pending
                        ? await _supplierFulfillmentService.ProcessAsync(requestResult.Value.FulfillmentId, cancellationToken)
                        : await _supplierFulfillmentService.ReconcileAsync(requestResult.Value.FulfillmentId, cancellationToken);

                    if (outcomeResult.IsFailure)
                    {
                        // Claim race lost on this exact candidate, or the candidate became unusable
                        // between the request and now — another worker/attempt owns this candidate's
                        // outcome; never treated as "try the next supplier" since a real attempt may
                        // already be in flight against it.
                        exhausted = false;
                        break;
                    }

                    var status = outcomeResult.Value.Status;
                    if (status is SupplierFulfillmentStatus.Succeeded or SupplierFulfillmentStatus.PartialFailed)
                    {
                        // Delivered codes (resolved here or, for PartialFailed's remainder, later by the
                        // existing sweep against this SAME supplier) are attached to OrderLicenseKey by
                        // CommerceOrderLicenseKeyDeliverySink, invoked by SupplierFulfillmentService
                        // itself — never here.
                        selectedCandidate = candidate;
                        exhausted = false;
                        break;
                    }

                    if (status == SupplierFulfillmentStatus.Failed)
                    {
                        // Definite, zero-delivered outcome — safe to consider the next cheapest candidate.
                        if (!isLastCandidate)
                        {
                            fallbackOccurred = true;
                        }

                        continue;
                    }

                    // Submitted/Submitting/Unknown — the outcome is ambiguous or still genuinely pending
                    // (e.g. Bamboo's always-async acceptance). Per the explicit "ambiguous → stop, never
                    // blindly try another supplier" requirement: this candidate is the one being resolved;
                    // the existing reconciliation sweep will finish the job. Recorded as selected (an
                    // attempt genuinely happened here) even though the terminal outcome isn't known yet.
                    selectedCandidate = candidate;
                    exhausted = false;
                    break;
                }
                catch (Exception ex)
                {
                    // An unexpected exception from RequestFulfillmentAsync/ProcessAsync/ReconcileAsync
                    // themselves (not a provider-reported outcome) is ambiguous by the same rule — stop,
                    // never fail over. The background sweep will retry this exact candidate.
                    _logger.LogWarning(
                        ex,
                        "Automated supplier fulfillment attempt failed unexpectedly for order {OrderId} item {OrderItemId}, supplier {SupplierId} — the background sweep will retry.",
                        order.Id, item.Id, candidate.SupplierId);
                    selectedCandidate = candidate;
                    exhausted = false;
                    break;
                }
            }

            if (anyAttemptQueued)
            {
                queued++;
            }

            if (exhausted)
            {
                // Every eligible candidate was tried and every one came back with a definite, zero-delivered failure.
                noSupplierAvailable++;
            }

            await RecordRoutingDecisionAsync(order.Id, item.Id, selectedCandidate, routing, fallbackOccurred, cancellationToken);
        }

        return new AutomatedSupplierFulfillmentSummary(shortfallLines, queued, noSupplierAvailable);
    }

    /// <summary>
    /// Best-effort admin-audit write — never allowed to fail the actual fulfillment attempt it's
    /// describing, mirroring <c>SupplierFulfillmentService.NotifyDeliverySinkAsync</c>'s identical
    /// "log and move on" posture for a non-critical side write. See <c>SupplierRoutingAuditLog</c>'s own
    /// remarks for why its contents are safe for an admin to see but must never be customer-facing.
    /// </summary>
    private async Task RecordRoutingDecisionAsync(
        Guid orderId,
        Guid orderItemId,
        SupplierPricingCandidate? selected,
        SupplierPricingResult routing,
        bool fallbackOccurred,
        CancellationToken cancellationToken)
    {
        try
        {
            var candidateSummaries = routing.RankedBySellingPriceAscending
                .Select(c => new SupplierRoutingCandidateSummaryDto(
                    c.SupplierName, c.ProviderType, Eligible: true, Selected: c.SupplierId == selected?.SupplierId && c.SupplierProductMappingId == selected?.SupplierProductMappingId,
                    c.CostInBaseCurrency, c.OriginalCurrency, c.OriginalCost, RejectionReason: null))
                .Concat(routing.Rejected.Select(r => new SupplierRoutingCandidateSummaryDto(
                    r.SupplierName, ProviderType: string.Empty, Eligible: false, Selected: false,
                    CostInBaseCurrency: null, OriginalCurrency: null, OriginalCost: null, RejectionReason: r.Reason)))
                .ToList();

            var candidatesJson = System.Text.Json.JsonSerializer.Serialize(candidateSummaries, JsonOptions);

            var log = SupplierRoutingAuditLog.Create(
                orderId, orderItemId, selected?.SupplierId, selected?.SupplierProductMappingId,
                selected?.CostInBaseCurrency, routing.BaseCurrency, fallbackOccurred, candidatesJson);

            _suppliersDb.SupplierRoutingAuditLogs.Add(log);
            await _suppliersDb.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to record supplier routing audit for order {OrderId} item {OrderItemId} — the fulfillment attempt itself is unaffected.",
                orderId, orderItemId);
        }
    }

    private static readonly System.Text.Json.JsonSerializerOptions JsonOptions = new(System.Text.Json.JsonSerializerDefaults.Web);
}
