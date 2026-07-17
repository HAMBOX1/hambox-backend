using HAMBOX.Application.Abstractions;
using HAMBOX.Application.Communication;
using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Catalog.Application.Errors;
using HAMBOX.Modules.Catalog.Domain.Enums;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Errors;
using HAMBOX.Modules.Commerce.Application.Services;
using HAMBOX.Modules.Commerce.Domain.Account;
using HAMBOX.Modules.Commerce.Domain.Enums;
using HAMBOX.Modules.Commerce.Domain.Orders;
using HAMBOX.Modules.Commerce.Domain.Promotions;
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
    private readonly ICommerceTransactionService _transactionService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IInventoryEngine _inventoryEngine;
    private readonly CartResponseBuilder _cartResponseBuilder;
    private readonly IEnumerable<IPaymentProvider> _paymentProviders;
    private readonly ICommunicationService _communicationService;
    private readonly ILogger<CheckoutCommandHandler> _logger;

    public CheckoutCommandHandler(
        ICommerceDbContext commerceDbContext,
        ICatalogDbContext catalogDbContext,
        ICommerceTransactionService transactionService,
        ICurrentUserService currentUserService,
        IInventoryEngine inventoryEngine,
        CartResponseBuilder cartResponseBuilder,
        IEnumerable<IPaymentProvider> paymentProviders,
        ICommunicationService communicationService,
        ILogger<CheckoutCommandHandler> logger)
    {
        _commerceDbContext = commerceDbContext;
        _catalogDbContext = catalogDbContext;
        _transactionService = transactionService;
        _currentUserService = currentUserService;
        _inventoryEngine = inventoryEngine;
        _cartResponseBuilder = cartResponseBuilder;
        _paymentProviders = paymentProviders;
        _communicationService = communicationService;
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

        foreach (var item in cart.Items)
        {
            if (!products.TryGetValue(item.ProductId, out var product))
            {
                return Result.Failure<Contracts.OrderDto>(CommerceErrors.ProductNotFound);
            }

            if (product.Status != ProductStatus.Active)
            {
                return Result.Failure<Contracts.OrderDto>(CatalogErrors.ProductNotActive);
            }

            if (item.ProductVariantId is Guid variantId)
            {
                if (!variants.TryGetValue(variantId, out var variant) || variant.Status != ProductVariantStatus.Active || !variant.IsVisible)
                {
                    return Result.Failure<Contracts.OrderDto>(CatalogErrors.VariantNotFound);
                }

                if (variant.ProductId != item.ProductId)
                {
                    return Result.Failure<Contracts.OrderDto>(CatalogErrors.VariantNotFound);
                }

                if (!variantStock.TryGetValue(variantId, out var stock) || stock.Available < item.Quantity)
                {
                    return Result.Failure<Contracts.OrderDto>(CatalogErrors.InsufficientInventory);
                }
            }
            else if (await _inventoryEngine.ProductHasVariantsAsync(item.ProductId, cancellationToken))
            {
                return Result.Failure<Contracts.OrderDto>(CatalogErrors.VariantRequired);
            }
            else if (product.AvailableStock < item.Quantity)
            {
                return Result.Failure<Contracts.OrderDto>(CatalogErrors.InsufficientStock);
            }
        }

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
        var reservedCodesByLine = new Dictionary<(Guid ProductId, Guid? VariantId), List<ReservedCodeSnapshot>>();

        try
        {
            PaymentProviderResult? paymentResult = null;

            await _transactionService.ExecuteAsync(async ct =>
            {
                foreach (var item in cart.Items)
                {
                    if (item.ProductVariantId is Guid variantId)
                    {
                        var reserved = await _inventoryEngine.ReserveCodesAsync(
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
                    else
                    {
                        products[item.ProductId].ReserveStock(item.Quantity);
                    }
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

                        return (
                            item.ProductId,
                            products[item.ProductId].NameEn,
                            item.Quantity,
                            item.UnitPrice,
                            variantId,
                            sku);
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
                order.Complete();

                foreach (var applied in evaluation.AppliedPromotions)
                {
                    _commerceDbContext.OrderAppliedPromotions.Add(OrderAppliedPromotion.Create(
                        order.Id,
                        applied.PromotionId,
                        applied.CouponCodeId,
                        applied.Name,
                        applied.Type,
                        applied.DiscountAmount,
                        applied.CouponCode));

                    _commerceDbContext.PromotionRedemptions.Add(PromotionRedemption.Create(
                        applied.PromotionId,
                        applied.CouponCodeId,
                        _currentUserService.UserId,
                        order.Id,
                        applied.DiscountAmount,
                        applied.Name));

                    var promotion = await _commerceDbContext.Promotions
                        .Include(p => p.CouponCodes)
                        .FirstOrDefaultAsync(p => p.Id == applied.PromotionId, ct);

                    promotion?.RecordRedemption();

                    if (applied.CouponCodeId is not null)
                    {
                        var coupon = promotion?.CouponCodes.FirstOrDefault(c => c.Id == applied.CouponCodeId);
                        coupon?.RecordUse();

                        _commerceDbContext.PromotionAuditLogs.Add(PromotionAuditLog.Create(
                            applied.PromotionId,
                            applied.CouponCodeId,
                            PromotionAuditAction.CouponRedeemed,
                            _currentUserService.UserId,
                            $"Coupon {applied.CouponCode} redeemed on order {order.OrderNumber}"));
                    }
                }

                var commitAssignments = new List<(Guid OrderItemId, Guid CodeId)>();
                var reservedCodeCursor = new Dictionary<(Guid ProductId, Guid? VariantId), int>();

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
                    else if (orderItem.ProductId is Guid productId)
                    {
                        for (var i = 0; i < orderItem.Quantity; i++)
                        {
                            var licenseKey = OrderLicenseKey.Create(
                                order.Id,
                                orderItem.Id,
                                productId,
                                LicenseKeyGenerator.Generate(),
                                orderItem.ProductVariantId);

                            _commerceDbContext.OrderLicenseKeys.Add(licenseKey);
                        }
                    }
                }

                if (commitAssignments.Count > 0)
                {
                    var committed = await _inventoryEngine.CommitReservationsAsync(order.Id, commitAssignments, ct);
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

                foreach (var item in cart.Items.Where(i => i.ProductVariantId is null))
                {
                    products[item.ProductId].CommitSale(item.Quantity);
                }

                _commerceDbContext.Orders.Add(order);
                cart.Clear();

                await _commerceDbContext.SaveChangesAsync(ct);
                await _catalogDbContext.SaveChangesAsync(ct);

                createdOrder = order;
            }, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Checkout failed while reserving or committing inventory.");

            if (IsInventoryFailure(ex))
            {
                return Result.Failure<Contracts.OrderDto>(CatalogErrors.InsufficientInventory);
            }

            if (ex.Message.Contains("payment", StringComparison.OrdinalIgnoreCase))
            {
                return Result.Failure<Contracts.OrderDto>(
                    new Error(CommerceErrors.PaymentFailed.Code, ex.Message));
            }

            return Result.Failure<Contracts.OrderDto>(
                new Error("Checkout.Failed", ex.Message));
        }

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

        return Result.Success(CommerceMapper.ToOrderDto(createdOrder));
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
