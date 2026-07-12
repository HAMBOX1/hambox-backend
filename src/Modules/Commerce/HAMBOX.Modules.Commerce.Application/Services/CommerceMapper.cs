using HAMBOX.Modules.Commerce.Application.Contracts;
using HAMBOX.Modules.Commerce.Application.Contracts.Account;
using HAMBOX.Modules.Commerce.Application.Promotions.Models;
using HAMBOX.Modules.Commerce.Domain.Carts;
using HAMBOX.Modules.Catalog.Domain.Inventory;
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
        IReadOnlyDictionary<Guid, ProductVariant> variants,
        PromotionEvaluationResult evaluation,
        IReadOnlyDictionary<Guid, (string? Platform, string? Edition, string? Region, string? Summary)>? variantAttributes = null)
    {
        var items = cart.Items
            .Select(item =>
            {
                products.TryGetValue(item.ProductId, out var product);
                var name = product?.NameEn ?? "Unknown Product";
                string? variantSku = null;
                string? platform = null;
                string? region = null;
                string? edition = null;
                string? variantSummary = null;

                if (item.ProductVariantId is Guid variantId)
                {
                    if (variants.TryGetValue(variantId, out var variant))
                    {
                        variantSku = variant.Sku;
                    }

                    if (variantAttributes is not null &&
                        variantAttributes.TryGetValue(variantId, out var attributes))
                    {
                        platform = attributes.Platform;
                        edition = attributes.Edition;
                        region = attributes.Region;
                        variantSummary = attributes.Summary;
                    }
                }

                return new CartItemDto(
                    item.ProductId,
                    item.ProductVariantId,
                    variantSku,
                    name,
                    item.Quantity,
                    item.UnitPrice,
                    item.UnitPrice * item.Quantity,
                    platform,
                    region,
                    edition,
                    variantSummary);
            })
            .ToList();

        var totals = new CartTotalsDto(
            evaluation.Subtotal,
            evaluation.TotalDiscount,
            evaluation.Tax,
            evaluation.Total,
            evaluation.ItemCount,
            evaluation.AppliedPromotions
                .Select(p => new Contracts.AppliedPromotionDto(
                    p.PromotionId,
                    p.Name,
                    p.Type.ToString(),
                    p.CouponCode,
                    p.DiscountAmount,
                    p.IsAutomatic,
                    p.Description))
                .ToList(),
            evaluation.ValidationErrors,
            cart.AppliedCouponCode);

        return new CartDto(
            cart.Id,
            guestSessionId ?? cart.GuestSessionId,
            items,
            totals);
    }

    public static CartDto ToEmptyCartDto(string? guestSessionId) =>
        new(
            null,
            guestSessionId,
            [],
            new CartTotalsDto(0m, 0m, 0m, 0m, 0, [], [], null));

    public static OrderDto ToOrderDto(Domain.Orders.Order order) =>
        new(
            order.Id,
            order.OrderNumber,
            order.Status.ToString(),
            order.Kind.ToString(),
            order.MembershipAction?.ToString(),
            order.Email,
            order.Country,
            order.PaymentMethod,
            order.PaymentProvider,
            order.PaymentTransactionId,
            order.PaymentStatus.ToString(),
            order.Subtotal,
            order.DiscountAmount,
            order.TaxAmount,
            order.TotalAmount,
            order.Items.Select(i => new OrderItemDto(
                i.ProductId,
                i.MembershipPlanId,
                i.LineItemType.ToString(),
                i.ProductNameEn,
                i.Quantity,
                i.UnitPrice,
                i.LineTotal)).ToList(),
            order.CreatedOnUtc);

    public static OrderDetailDto ToOrderDetailDto(
        Domain.Orders.Order order,
        IReadOnlyList<Domain.Account.OrderLicenseKey> licenseKeys,
        IReadOnlyDictionary<Guid, Domain.Account.ProductReview> reviewsByProductId,
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
                i.MembershipPlanId,
                i.LineItemType.ToString(),
                i.ProductNameEn,
                i.Quantity,
                i.UnitPrice,
                i.LineTotal)).ToList(),
            AccountMapper.BuildOrderTimeline(order),
            licenseKeys.Select(k =>
            {
                var item = order.Items.First(i => i.Id == k.OrderItemId);
                return new Contracts.Account.OrderLicenseKeyDto(
                    k.OrderItemId,
                    k.ProductId,
                    item.ProductNameEn,
                    AdminOrderMapper.MaskLicenseKey(k.LicenseKey),
                    k.Id);
            }).ToList(),
            invoiceUrl,
            supportUrl,
            order.Items.Where(i => i.ProductId is Guid).Select(i =>
            {
                reviewsByProductId.TryGetValue(i.ProductId!.Value, out var review);
                var canReview = order.Status == Domain.Enums.OrderStatus.Completed && review is null;
                return new Contracts.Account.OrderItemReviewStatusDto(
                    i.Id,
                    i.ProductId!.Value,
                    i.ProductNameEn,
                    canReview,
                    review is not null,
                    review?.Id);
            }).ToList(),
            order.CreatedOnUtc);
}
