using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Catalog.Application.Errors;
using HAMBOX.Modules.Catalog.Domain.Enums;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Errors;
using HAMBOX.Modules.Commerce.Application.Services;
using HAMBOX.Modules.Commerce.Domain.Account;
using HAMBOX.Modules.Commerce.Domain.Orders;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Commerce.Application.Features.Checkout;

internal sealed class CheckoutCommandHandler : IRequestHandler<CheckoutCommand, Result<Contracts.OrderDto>>
{
    private readonly ICommerceDbContext _commerceDbContext;
    private readonly ICatalogDbContext _catalogDbContext;
    private readonly ICommerceTransactionService _transactionService;
    private readonly ICurrentUserService _currentUserService;

    public CheckoutCommandHandler(
        ICommerceDbContext commerceDbContext,
        ICatalogDbContext catalogDbContext,
        ICommerceTransactionService transactionService,
        ICurrentUserService currentUserService)
    {
        _commerceDbContext = commerceDbContext;
        _catalogDbContext = catalogDbContext;
        _transactionService = transactionService;
        _currentUserService = currentUserService;
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

        var productIds = cart.Items.Select(i => i.ProductId).ToList();
        var products = await _catalogDbContext.Products
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, cancellationToken);

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

            if (product.AvailableStock < item.Quantity)
            {
                return Result.Failure<Contracts.OrderDto>(CatalogErrors.InsufficientStock);
            }
        }

        var (subtotal, discountAmount, taxAmount, totalAmount) =
            CartTotalsCalculator.CalculateOrderAmounts(cart.Items, isAuthenticated: true);

        Order? createdOrder = null;

        try
        {
            await _transactionService.ExecuteAsync(async ct =>
            {
                foreach (var item in cart.Items)
                {
                    products[item.ProductId].ReserveStock(item.Quantity);
                }

                await _catalogDbContext.SaveChangesAsync(ct);

                var orderNumber = $"ORD-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";
                var orderItems = cart.Items
                    .Select(item => (
                        item.ProductId,
                        products[item.ProductId].NameEn,
                        item.Quantity,
                        item.UnitPrice))
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

                order.Complete();

                foreach (var orderItem in order.Items)
                {
                    for (var i = 0; i < orderItem.Quantity; i++)
                    {
                        var licenseKey = OrderLicenseKey.Create(
                            order.Id,
                            orderItem.Id,
                            orderItem.ProductId,
                            LicenseKeyGenerator.Generate());

                        _commerceDbContext.OrderLicenseKeys.Add(licenseKey);
                    }
                }

                var notification = UserNotification.Create(
                    _currentUserService.UserId!,
                    "Order confirmed",
                    $"Your order {order.OrderNumber} is complete. License keys are ready.",
                    "Order");

                _commerceDbContext.UserNotifications.Add(notification);

                foreach (var item in cart.Items)
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
        catch (InvalidOperationException)
        {
            return Result.Failure<Contracts.OrderDto>(CatalogErrors.InsufficientStock);
        }

        return Result.Success(CommerceMapper.ToOrderDto(createdOrder!));
    }
}
