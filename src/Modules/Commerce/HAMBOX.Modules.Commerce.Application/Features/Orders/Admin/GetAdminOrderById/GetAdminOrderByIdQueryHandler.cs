using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Contracts.Orders;
using HAMBOX.Modules.Commerce.Application.Errors;
using HAMBOX.Modules.Commerce.Application.Services;
using HAMBOX.Modules.Commerce.Domain.Account;
using HAMBOX.Modules.Commerce.Domain.Enums;
using HAMBOX.Modules.Commerce.Domain.Orders;
using HAMBOX.Modules.Commerce.Domain.Promotions;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Commerce.Application.Features.Orders.Admin.GetAdminOrderById;

public sealed record GetAdminOrderByIdQuery(Guid OrderId) : IRequest<Result<AdminOrderDetailDto>>;

internal sealed class GetAdminOrderByIdQueryHandler
    : IRequestHandler<GetAdminOrderByIdQuery, Result<AdminOrderDetailDto>>
{
    private readonly ICommerceDbContext _commerceDbContext;
    private readonly ICatalogDbContext _catalogDbContext;

    public GetAdminOrderByIdQueryHandler(
        ICommerceDbContext commerceDbContext,
        ICatalogDbContext catalogDbContext)
    {
        _commerceDbContext = commerceDbContext;
        _catalogDbContext = catalogDbContext;
    }

    public async Task<Result<AdminOrderDetailDto>> Handle(
        GetAdminOrderByIdQuery request,
        CancellationToken cancellationToken)
    {
        var order = await _commerceDbContext.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == request.OrderId, cancellationToken);

        if (order is null)
        {
            return Result.Failure<AdminOrderDetailDto>(CommerceErrors.OrderNotFound);
        }

        var promotions = await _commerceDbContext.OrderAppliedPromotions
            .AsNoTracking()
            .Where(p => p.OrderId == order.Id)
            .ToListAsync(cancellationToken);

        var licenseKeys = await _commerceDbContext.OrderLicenseKeys
            .AsNoTracking()
            .Where(k => k.OrderId == order.Id)
            .ToListAsync(cancellationToken);

        var notes = await _commerceDbContext.OrderAdminNotes
            .AsNoTracking()
            .Where(n => n.OrderId == order.Id)
            .OrderByDescending(n => n.CreatedOnUtc)
            .ToListAsync(cancellationToken);

        var auditEntries = await _commerceDbContext.OrderAuditEntries
            .AsNoTracking()
            .Where(e => e.OrderId == order.Id)
            .OrderBy(e => e.OccurredOnUtc)
            .ToListAsync(cancellationToken);

        var paymentCallbacks = await _commerceDbContext.OrderPaymentCallbacks
            .AsNoTracking()
            .Where(c => c.OrderId == order.Id)
            .OrderByDescending(c => c.OccurredOnUtc)
            .ToListAsync(cancellationToken);

        var customerOrders = await _commerceDbContext.Orders
            .AsNoTracking()
            .Where(o => o.Email == order.Email && o.Id != order.Id)
            .OrderByDescending(o => o.CreatedOnUtc)
            .Take(10)
            .ToListAsync(cancellationToken);

        var customerOrderIds = customerOrders.Select(o => o.Id).ToList();
        var customerLicenseKeys = customerOrderIds.Count > 0
            ? await _commerceDbContext.OrderLicenseKeys
                .AsNoTracking()
                .Where(k => customerOrderIds.Contains(k.OrderId))
                .ToListAsync(cancellationToken)
            : [];

        var customerKeysByOrder = customerLicenseKeys
            .GroupBy(k => k.OrderId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<OrderLicenseKey>)g.ToList());

        var productIds = order.Items.Where(i => i.ProductId is Guid).Select(i => i.ProductId!.Value).Distinct().ToList();
        var imageUrls = await ProductPrimaryImageResolver.GetPrimaryImageUrlsAsync(
            _catalogDbContext,
            productIds,
            cancellationToken);

        var membershipDiscount = AdminOrderMapper.ResolveMembershipDiscount(promotions);
        var coupon = promotions.FirstOrDefault(p => !string.IsNullOrWhiteSpace(p.CouponCode));
        var keysByItem = licenseKeys.GroupBy(k => k.OrderItemId).ToDictionary(g => g.Key, g => g.ToList());

        var items = order.Items.Select(item =>
        {
            keysByItem.TryGetValue(item.Id, out var itemKeys);
            var delivered = itemKeys?.Count ?? 0;
            return new AdminOrderItemDto(
                item.Id,
                item.ProductId ?? Guid.Empty,
                item.ProductVariantId,
                item.ProductNameEn,
                item.VariantSku,
                item.ProductId is Guid pid && imageUrls.TryGetValue(pid, out var imageUrl) ? imageUrl : null,
                item.Quantity,
                item.UnitPrice,
                0m,
                item.LineTotal,
                delivered,
                AdminOrderMapper.ResolveItemDeliveryStatus(item.Quantity, delivered));
        }).ToList();

        var maskedKeys = licenseKeys.Select(key =>
        {
            var item = order.Items.First(i => i.Id == key.OrderItemId);
            return new AdminOrderLicenseKeyDto(
                key.Id,
                key.OrderItemId,
                key.ProductId,
                item.ProductNameEn,
                AdminOrderMapper.MaskLicenseKey(key.LicenseKey),
                AdminOrderMapper.ResolveItemDeliveryStatus(item.Quantity, 1),
                order.ModifiedOnUtc ?? order.CreatedOnUtc,
                true);
        }).ToList();

        return Result.Success(new AdminOrderDetailDto(
            order.Id,
            order.OrderNumber,
            AdminOrderMapper.ResolveOrderStatusLabel(order),
            AdminOrderMapper.ResolvePaymentStatusLabel(order.PaymentStatus),
            AdminOrderMapper.ResolveDeliveryStatus(order, licenseKeys),
            order.Email,
            AdminOrderMapper.ResolveCustomerName(order.Email),
            order.Country,
            order.PaymentMethod,
            order.PaymentProvider,
            order.PaymentTransactionId,
            order.Subtotal,
            order.DiscountAmount,
            membershipDiscount,
            order.TaxAmount,
            order.TotalAmount,
            coupon?.CouponCode,
            coupon?.PromotionName,
            items,
            AdminOrderMapper.BuildTimeline(order, auditEntries),
            maskedKeys,
            notes.Select(n => new AdminOrderAdminNoteDto(
                n.Id,
                n.Body,
                n.AuthorDisplayName,
                n.CreatedOnUtc,
                n.ModifiedOnUtc)).ToList(),
            auditEntries.Select(e => new AdminOrderAuditEntryDto(
                e.Id,
                e.EventType,
                e.Description,
                e.ActorDisplayName,
                e.OccurredOnUtc)).ToList(),
            paymentCallbacks.Select(c => new AdminOrderPaymentCallbackDto(
                c.Id,
                c.Provider,
                c.EventType,
                c.Status,
                c.TransactionId,
                c.PayloadJson,
                c.OccurredOnUtc)).ToList(),
            customerOrders.Select(o =>
            {
                customerKeysByOrder.TryGetValue(o.Id, out var keys);
                keys ??= [];
                return new AdminCustomerOrderHistoryItemDto(
                    o.Id,
                    o.OrderNumber,
                    o.TotalAmount,
                    AdminOrderMapper.ResolveOrderStatusLabel(o),
                    AdminOrderMapper.ResolvePaymentStatusLabel(o.PaymentStatus),
                    AdminOrderMapper.ResolveDeliveryStatus(o, keys),
                    o.CreatedOnUtc);
            }).ToList(),
            null,
            order.CreatedOnUtc,
            order.LastEditedByName,
            order.LastEditedOnUtc));
    }
}
