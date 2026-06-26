using HAMBOX.Modules.Commerce.Application.Contracts;
using HAMBOX.Modules.Commerce.Application.Contracts.Account;
using HAMBOX.Modules.Commerce.Domain.Account;
using HAMBOX.Modules.Commerce.Domain.Carts;
using HAMBOX.Modules.Catalog.Domain.Products;

namespace HAMBOX.Modules.Commerce.Application.Services;

/// <summary>
/// Maps commerce domain entities to DTOs.
/// </summary>
internal static class CommerceMapper
{
    public static CartDto ToCartDto(
        ShoppingCart cart,
        string? guestSessionId,
        IReadOnlyDictionary<Guid, Product> products,
        bool isAuthenticated)
    {
        var items = cart.Items
            .Select(item =>
            {
                products.TryGetValue(item.ProductId, out var product);
                var name = product?.NameEn ?? "Unknown Product";
                return new CartItemDto(
                    item.ProductId,
                    name,
                    item.Quantity,
                    item.UnitPrice,
                    item.UnitPrice * item.Quantity);
            })
            .ToList();

        var totals = CartTotalsCalculator.Calculate(cart.Items, isAuthenticated);

        return new CartDto(
            cart.Id,
            guestSessionId ?? cart.GuestSessionId,
            items,
            totals);
    }

    public static OrderDto ToOrderDto(Domain.Orders.Order order) =>
        new(
            order.Id,
            order.OrderNumber,
            order.Status.ToString(),
            order.Email,
            order.Country,
            order.PaymentMethod,
            order.Subtotal,
            order.DiscountAmount,
            order.TaxAmount,
            order.TotalAmount,
            order.Items.Select(i => new OrderItemDto(
                i.ProductId,
                i.ProductNameEn,
                i.Quantity,
                i.UnitPrice,
                i.LineTotal)).ToList(),
            order.CreatedOnUtc);

    public static OrderDetailDto ToOrderDetailDto(
        Domain.Orders.Order order,
        IReadOnlyList<OrderLicenseKey> licenseKeys,
        IReadOnlyDictionary<Guid, ProductReview> reviewsByProductId,
        string? invoiceUrl,
        string? supportUrl) =>
        new(
            order.Id,
            order.OrderNumber,
            order.Status.ToString(),
            order.Email,
            order.Country,
            order.PaymentMethod,
            order.Subtotal,
            order.DiscountAmount,
            order.TaxAmount,
            order.TotalAmount,
            order.Items.Select(i => new OrderItemDto(
                i.ProductId,
                i.ProductNameEn,
                i.Quantity,
                i.UnitPrice,
                i.LineTotal)).ToList(),
            AccountMapper.BuildOrderTimeline(order),
            licenseKeys.Select(k =>
            {
                var item = order.Items.First(i => i.Id == k.OrderItemId);
                return new OrderLicenseKeyDto(k.OrderItemId, k.ProductId, item.ProductNameEn, k.LicenseKey);
            }).ToList(),
            invoiceUrl,
            supportUrl,
            order.Items.Select(i =>
            {
                reviewsByProductId.TryGetValue(i.ProductId, out var review);
                var canReview = order.Status == Domain.Enums.OrderStatus.Completed && review is null;
                return new OrderItemReviewStatusDto(
                    i.Id,
                    i.ProductId,
                    i.ProductNameEn,
                    canReview,
                    review is not null,
                    review?.Id);
            }).ToList(),
            order.CreatedOnUtc);
}
