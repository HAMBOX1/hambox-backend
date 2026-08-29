using HAMBOX.Application.Abstractions;
using HAMBOX.Application.Communication;
using HAMBOX.Application.Membership;
using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Catalog.Application.Errors;
using HAMBOX.Modules.Catalog.Domain.Enums;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Errors;
using HAMBOX.Modules.Commerce.Application.Referrals;
using HAMBOX.Modules.Commerce.Application.Services;
using HAMBOX.Modules.Commerce.Domain.Account;
using HAMBOX.Modules.Commerce.Domain.Enums;
using HAMBOX.Modules.Commerce.Domain.Operations;
using HAMBOX.Modules.Commerce.Domain.Orders;
using HAMBOX.Modules.Legal.Application.Abstractions;
using HAMBOX.Modules.Legal.Application.Services;
using HAMBOX.SharedKernel.Errors;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HAMBOX.Modules.Commerce.Application.Features.Checkout;

internal sealed class CheckoutCommandHandler : IRequestHandler<CheckoutCommand, Result<Contracts.OrderDto>>
{
    private readonly ICommerceDbContext _commerceDbContext;
    private readonly ICatalogDbContext _catalogDbContext;
    private readonly ILegalDbContext _legalDbContext;
    private readonly ICommerceTransactionService _transactionService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IInventoryEngine _inventoryEngine;
    private readonly CartResponseBuilder _cartResponseBuilder;
    private readonly CartLineValidator _cartLineValidator;
    private readonly PromotionRedemptionService _promotionRedemptionService;
    private readonly IEnumerable<IPaymentProvider> _paymentProviders;
    private readonly ICommunicationService _communicationService;
    private readonly IMembershipAccessProvider _membershipAccess;
    private readonly ReferralLifecycleService _referralLifecycle;
    private readonly IOperationalJobQueue _jobQueue;
    private readonly ILogger<CheckoutCommandHandler> _logger;

    public CheckoutCommandHandler(
        ICommerceDbContext commerceDbContext,
        ICatalogDbContext catalogDbContext,
        ILegalDbContext legalDbContext,
        ICommerceTransactionService transactionService,
        ICurrentUserService currentUserService,
        IInventoryEngine inventoryEngine,
        CartResponseBuilder cartResponseBuilder,
        CartLineValidator cartLineValidator,
        PromotionRedemptionService promotionRedemptionService,
        IEnumerable<IPaymentProvider> paymentProviders,
        ICommunicationService communicationService,
        IMembershipAccessProvider membershipAccess,
        ReferralLifecycleService referralLifecycle,
        IOperationalJobQueue jobQueue,
        ILogger<CheckoutCommandHandler> logger)
    {
        _commerceDbContext = commerceDbContext;
        _catalogDbContext = catalogDbContext;
        _legalDbContext = legalDbContext;
        _transactionService = transactionService;
        _currentUserService = currentUserService;
        _inventoryEngine = inventoryEngine;
        _cartResponseBuilder = cartResponseBuilder;
        _cartLineValidator = cartLineValidator;
        _promotionRedemptionService = promotionRedemptionService;
        _paymentProviders = paymentProviders;
        _communicationService = communicationService;
        _membershipAccess = membershipAccess;
        _referralLifecycle = referralLifecycle;
        _jobQueue = jobQueue;
        _logger = logger;
    }

    public async Task<Result<Contracts.OrderDto>> Handle(CheckoutCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUserService.IsAuthenticated || _currentUserService.UserId is null)
        {
            return Result.Failure<Contracts.OrderDto>(CommerceErrors.AuthenticationRequired);
        }

        var cart = await _commerceDbContext.ShoppingCarts
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.UserId == _currentUserService.UserId, cancellationToken);

        if (cart is null || cart.Items.Count == 0)
        {
            return Result.Failure<Contracts.OrderDto>(CommerceErrors.CartEmpty);
        }

        var access = await _membershipAccess.GetAccessInfoAsync(_currentUserService.UserId, cancellationToken);
        if (access.MaxPurchasesPerMonth is int monthlyLimit)
        {
            var startOfMonthUtc = new DateTimeOffset(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, TimeSpan.Zero);
            var purchasesThisMonth = await _commerceDbContext.Orders.CountAsync(
                o => o.UserId == _currentUserService.UserId
                    && o.Status == OrderStatus.Completed
                    && o.CreatedOnUtc >= startOfMonthUtc,
                cancellationToken);

            if (purchasesThisMonth >= monthlyLimit)
            {
                return Result.Failure<Contracts.OrderDto>(CommerceErrors.MembershipPurchaseLimitExceeded(monthlyLimit));
            }
        }

        await _inventoryEngine.ExpireStaleReservationsAsync(cancellationToken);
        await _inventoryEngine.ReleaseReservationsForCartAsync(cart.Id, cancellationToken);

        var productIds = cart.Items.Select(i => i.ProductId).ToList();
        var products = await _catalogDbContext.Products
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        var variantIds = cart.Items.Where(i => i.ProductVariantId.HasValue).Select(i => i.ProductVariantId!.Value).Distinct().ToList();
        var variants = variantIds.Count > 0
            ? await _catalogDbContext.ProductVariants
                .Where(v => variantIds.Contains(v.Id))
                .ToDictionaryAsync(v => v.Id, cancellationToken)
            : new Dictionary<Guid, HAMBOX.Modules.Catalog.Domain.Inventory.ProductVariant>();

        var variantStock = variantIds.Count > 0
            ? await _inventoryEngine.GetVariantStockBulkAsync(variantIds, cancellationToken)
            : new Dictionary<Guid, VariantStockSnapshot>();

        var productAccess = await _membershipAccess.GetProductsAccessAsync(
            _currentUserService.UserId, productIds, cancellationToken);

        var lineValidation = await _cartLineValidator.ValidateAsync(
            cart, products, variants, variantStock, productAccess, access, cancellationToken);
        if (lineValidation.IsFailure)
        {
            return Result.Failure<Contracts.OrderDto>(lineValidation.Error);
        }

        CartLineValidator.ApplyResolvedPricing(cart, lineValidation.Value);
        var resolvedPricingByLine = lineValidation.Value.Lines.ToDictionary(l => (l.ProductId, l.ProductVariantId));

        var (subtotal, discountAmount, taxAmount, totalAmount, evaluation) =
            await _cartResponseBuilder.BuildOrderAmountsAsync(cart, request.Country, cancellationToken);

        if (evaluation.ValidationErrors.Count > 0)
        {
            return Result.Failure<Contracts.OrderDto>(
                CommerceErrors.InvalidCoupon(string.Join(' ', evaluation.ValidationErrors)));
        }

        var provider = _paymentProviders.FirstOrDefault(p => p.CanHandle(request.PaymentMethod));
        if (provider is null)
        {
            return Result.Failure<Contracts.OrderDto>(CommerceErrors.PaymentMethodNotSupported);
        }

        Order? createdOrder = null;
        var needsAutomatedSupplierFulfillment = false;
        var reservedCodesByLine = new Dictionary<(Guid ProductId, Guid? VariantId), List<ReservedCodeSnapshot>>();

        try
        {
            PaymentProviderResult? paymentResult = null;

            await _transactionService.ExecuteAsync(async ct =>
            {
                // Every cart line is guaranteed to carry a ProductVariantId here: the validation
                // loop above already rejected any variant-less line, so reservation is always
                // against real, inventory-backed digital codes.
                foreach (var item in cart.Items)
                {
                    var variantId = item.ProductVariantId!.Value;
                    var mode = variants.TryGetValue(variantId, out var variantForMode)
                        ? variantForMode.FulfillmentMode
                        : FulfillmentMode.ManualOnly;

                    if (mode is FulfillmentMode.SupplierFirst or FulfillmentMode.SupplierOnly)
                    {
                        // Never reserve manual inventory inline for these modes — CartLineValidator
                        // already confirmed a READY supplier route exists; the automated-supplier step,
                        // run strictly after this transaction commits, covers the full quantity.
                        continue;
                    }

                    // ManualOnly/ManualFirst: reserve whatever manual stock actually exists, up to the
                    // requested quantity — never throws on a shortfall. CartLineValidator already
                    // confirmed the shortfall (if any) is covered by a READY supplier for ManualFirst;
                    // for ManualOnly a genuine shortfall here would mean CartLineValidator's own stock
                    // check has a bug, since it required full manual sufficiency for that mode.
                    var reserved = await _inventoryEngine.ReservePartialCodesAsync(
                        variantId,
                        item.Quantity,
                        _currentUserService.UserId,
                        cart.Id,
                        ct);

                    var lineKey = (item.ProductId, item.ProductVariantId);
                    if (!reservedCodesByLine.TryGetValue(lineKey, out var reservedList))
                    {
                        reservedList = [];
                        reservedCodesByLine[lineKey] = reservedList;
                    }

                    reservedList.AddRange(reserved);
                }

                await _catalogDbContext.SaveChangesAsync(ct);

                paymentResult = await provider.ProcessAsync(
                    new PaymentProviderRequest(
                        totalAmount,
                        "USD",
                        request.PaymentMethod,
                        request.Email,
                        _currentUserService.UserId),
                    ct);

                if (!paymentResult.IsSuccess)
                {
                    throw new InvalidOperationException(
                        paymentResult.FailureReason ?? CommerceErrors.PaymentFailed.Description);
                }

                var orderNumber = $"ORD-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";
                var orderItems = cart.Items
                    .Select(item =>
                    {
                        var variantId = item.ProductVariantId;
                        string? sku = null;
                        if (variantId is not null && variants.TryGetValue(variantId.Value, out var variant))
                        {
                            sku = variant.Sku;
                        }

                        var resolved = resolvedPricingByLine[(item.ProductId, variantId)];

                        return (
                            item.ProductId,
                            products[item.ProductId].NameEn,
                            item.Quantity,
                            item.UnitPrice,
                            variantId,
                            sku,
                            resolved.SelectedSupplierId,
                            resolved.SelectedSupplierProductMappingId,
                            resolved.SupplierBuyingPriceAtOrderTime,
                            resolved.MarginPercentAppliedAtOrderTime);
                    })
                    .ToList();

                var order = Order.Create(
                    _currentUserService.UserId!,
                    orderNumber,
                    request.Email,
                    request.Country,
                    request.PaymentMethod,
                    subtotal,
                    discountAmount,
                    taxAmount,
                    totalAmount,
                    orderItems);

                order.RecordPayment(paymentResult!.Provider, paymentResult.TransactionId!);
                OrderPaymentCallbackRecorder.Record(_commerceDbContext, order.Id, paymentResult);

                await _promotionRedemptionService.RedeemAsync(
                    order, evaluation.AppliedPromotions, _currentUserService.UserId, ct);

                var commitAssignments = new List<(Guid OrderItemId, Guid CodeId)>();
                var reservedCodeCursor = new Dictionary<(Guid ProductId, Guid? VariantId), int>();

                // Every order line resolved to a reserved, inventory-backed variant above, so
                // every line key here is guaranteed to have reserved codes — there is no
                // remaining branch that fabricates a license key with no real backing.
                foreach (var orderItem in order.Items.Where(i => i.ProductId is Guid))
                {
                    var lineKey = (orderItem.ProductId!.Value, orderItem.ProductVariantId);
                    if (reservedCodesByLine.TryGetValue(lineKey, out var reservedCodes))
                    {
                        if (!reservedCodeCursor.TryGetValue(lineKey, out var cursor))
                        {
                            cursor = 0;
                        }

                        for (var i = 0; i < orderItem.Quantity && cursor < reservedCodes.Count; i++, cursor++)
                        {
                            commitAssignments.Add((orderItem.Id, reservedCodes[cursor].CodeId));
                        }

                        reservedCodeCursor[lineKey] = cursor;
                    }
                }

                var manuallyDeliveredCount = 0;
                if (commitAssignments.Count > 0)
                {
                    var committed = await _inventoryEngine.CommitReservationsAsync(order.Id, commitAssignments, ct);
                    manuallyDeliveredCount = committed.Count;
                    foreach (var code in committed)
                    {
                        var orderItem = order.Items.First(i => i.Id == code.OrderItemId);
                        var licenseKey = OrderLicenseKey.Create(
                            order.Id,
                            orderItem.Id,
                            orderItem.ProductId!.Value,
                            code.DigitalCode,
                            orderItem.ProductVariantId,
                            code.CodeId);

                        _commerceDbContext.OrderLicenseKeys.Add(licenseKey);
                    }
                }

                // Order.Complete() must never fire before every required digital unit actually has a
                // license key — SupplierFirst/SupplierOnly lines were deliberately skipped above (see
                // the reservation loop's own comment) and are never covered by manual reservation, so
                // completing unconditionally here would mark the order Completed before the automated
                // supplier step (now a background job, queued below, strictly after this transaction
                // commits) has even attempted a purchase. Mirrors the exact same
                // "count keys vs required, complete only if fully covered, else MarkProcessing" check
                // OrderFulfillmentService.FulfillMissingAsync and CommerceOrderLicenseKeyDeliverySink
                // already use for their own completion decisions — not a new rule, the same one applied
                // a third time, consistently.
                var requiredQuantity = order.Items
                    .Where(i => i.LineItemType == OrderLineItemType.Product)
                    .Sum(i => i.Quantity);
                if (requiredQuantity > 0 && manuallyDeliveredCount >= requiredQuantity)
                {
                    order.Complete();

                    // Awards the referrer's points if this order qualifies (the referred user's first
                    // completed order) — points-only, nothing priced into this order depends on the
                    // outcome. Only fired here on the fast (fully manual) completion path; the job
                    // below fires the same call for anything that completes later via automated supply.
                    await _referralLifecycle.ProcessOrderCompletedAsync(order, ct);
                }
                else
                {
                    order.MarkProcessing();
                    needsAutomatedSupplierFulfillment = true;
                }

                _commerceDbContext.Orders.Add(order);
                cart.Clear();

                await _commerceDbContext.SaveChangesAsync(ct);
                await _catalogDbContext.SaveChangesAsync(ct);

                createdOrder = order;
            }, cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            // Legacy Product.StockQuantity/RowVersion race (optimistic concurrency, not the
            // row-locked digital-code path) — a genuine concurrent write lost the race. Map to
            // the same friendly, existing business error other product-concurrency conflicts use
            // instead of letting it surface as an unhandled 500.
            _logger.LogWarning(ex, "Checkout failed due to a concurrent product update.");
            return Result.Failure<Contracts.OrderDto>(CatalogErrors.ProductConcurrencyConflict);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Checkout failed while reserving or committing inventory.");

            if (IsInventoryFailure(ex))
            {
                return Result.Failure<Contracts.OrderDto>(CatalogErrors.InsufficientInventory);
            }

            if (ex.Message.Contains("coupon usage limit", StringComparison.OrdinalIgnoreCase))
            {
                return Result.Failure<Contracts.OrderDto>(
                    CommerceErrors.InvalidCoupon("This coupon has reached its usage limit."));
            }

            if (ex.Message.Contains("promotion usage limit", StringComparison.OrdinalIgnoreCase))
            {
                return Result.Failure<Contracts.OrderDto>(
                    CommerceErrors.InvalidCoupon("This promotion has reached its usage limit."));
            }

            if (ex.Message.Contains("payment", StringComparison.OrdinalIgnoreCase))
            {
                return Result.Failure<Contracts.OrderDto>(
                    new Error(CommerceErrors.PaymentFailed.Code, ex.Message));
            }

            return Result.Failure<Contracts.OrderDto>(
                new Error("Checkout.Failed", ex.Message));
        }

        // Job → Worker, not inline: the automated-supplier step makes real outbound calls to whichever
        // provider is cheapest-eligible (Bamboo/Visoria/GlobeTopper/Eneba/CodesWholesale), and must
        // never block the checkout HTTP response on that. Enqueued strictly after the payment
        // transaction above has committed — see OrderFulfillmentService.QueueAutomatedSupplierFulfillmentAsync's
        // remarks for why that call itself must never run inside a transaction; enqueueing here instead
        // of calling it is the only thing that changed — ExecuteOrderFulfillmentJobHandler calls that
        // exact same method, unmodified, once the worker claims this job. Only enqueued when manual
        // reservation didn't already cover every line (see the completion decision above) — a pure
        // ManualOnly order with full stock needs no automated-supplier attempt at all.
        if (needsAutomatedSupplierFulfillment)
        {
            await _jobQueue.EnqueueAsync(
                OperationalJobTypes.ExecuteOrderFulfillment,
                priority: OperationalJobPriority.High,
                relatedEntityType: "Order",
                relatedEntityId: createdOrder!.Id.ToString(),
                cancellationToken: cancellationToken);
        }

        // Compliance-audit row, not money/inventory-critical — same "sequential, not
        // cross-schema-atomic" treatment RegisterCommandHandler gives this. Ties the acceptance of
        // every currently-published, acceptance-required legal section to this order, per contract
        // §33.1 (User ID, Order ID, Policy Version, Timestamp, IP, Device).
        await LegalAcceptanceRecorder.RecordAsync(
            _legalDbContext,
            _currentUserService.UserId!,
            request.IpAddress,
            request.UserAgent,
            request.Language,
            createdOrder!.Id,
            cancellationToken);
        await _legalDbContext.SaveChangesAsync(cancellationToken);

        await _communicationService.SendAsync(new CommunicationRequest(
            UserId: _currentUserService.UserId!,
            TemplateKey: "OrderConfirmation",
            Category: CommunicationCategory.Order,
            Variables: new Dictionary<string, string>
            {
                ["OrderNumber"] = createdOrder!.OrderNumber,
                ["Total"] = createdOrder.TotalAmount.ToString("0.00"),
            },
            RelatedEntityType: "Order",
            RelatedEntityId: createdOrder.Id.ToString(),
            ActionUrl: $"/account/library?orderId={createdOrder.Id}"), cancellationToken);

        var imageUrls = await ProductPrimaryImageResolver.GetPrimaryImageUrlsAsync(
            _catalogDbContext,
            createdOrder.Items.Where(i => i.ProductId is not null).Select(i => i.ProductId!.Value).Distinct().ToList(),
            cancellationToken);

        return Result.Success(CommerceMapper.ToOrderDto(createdOrder, imageUrls));
    }

    private static bool IsInventoryFailure(InvalidOperationException ex)
    {
        var message = ex.Message;

        return message.Contains("insufficient", StringComparison.OrdinalIgnoreCase)
            || message.Contains("not available", StringComparison.OrdinalIgnoreCase)
            || message.Contains("must be reserved", StringComparison.OrdinalIgnoreCase)
            || message.Contains("expired", StringComparison.OrdinalIgnoreCase);
    }
}
