using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Contracts.Orders;
using HAMBOX.Modules.Commerce.Application.Services;
using HAMBOX.Modules.Commerce.Domain.Account;
using HAMBOX.Modules.Commerce.Domain.Enums;
using HAMBOX.Modules.Commerce.Domain.Orders;
using HAMBOX.Modules.Commerce.Domain.Promotions;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Commerce.Application.Features.Orders.Admin.GetAdminOrders;

public sealed record GetAdminOrdersQuery(
    int PageNumber,
    int PageSize,
    string? SearchTerm,
    string? OrderStatus,
    string? DeliveryStatus,
    string? PaymentStatus,
    string? DateRange,
    DateTimeOffset? DateFrom,
    DateTimeOffset? DateTo,
    decimal? MinAmount,
    decimal? MaxAmount,
    string? Sort,
    string? OrderKind) : IRequest<Result<PagedResult<AdminOrderListItemDto>>>;

internal sealed class GetAdminOrdersQueryHandler
    : IRequestHandler<GetAdminOrdersQuery, Result<PagedResult<AdminOrderListItemDto>>>
{
    private readonly ICommerceDbContext _dbContext;

    public GetAdminOrdersQueryHandler(ICommerceDbContext dbContext) => _dbContext = dbContext;

    public async Task<Result<PagedResult<AdminOrderListItemDto>>> Handle(
        GetAdminOrdersQuery request,
        CancellationToken cancellationToken)
    {
        var pageNumber = Math.Max(1, request.PageNumber);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var query = _dbContext.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .AsQueryable();

        query = ApplySearch(query, request.SearchTerm);
        query = ApplyOrderKindFilter(query, request.OrderKind);
        query = ApplyOrderStatusFilter(query, request.OrderStatus);
        query = ApplyPaymentStatusFilter(query, request.PaymentStatus);
        query = ApplyAmountFilter(query, request.MinAmount, request.MaxAmount);
        query = ApplyDateFilter(query, request.DateRange, request.DateFrom, request.DateTo);
        query = ApplySort(query, request.Sort);

        var hasDeliveryStatusFilter = !string.IsNullOrWhiteSpace(request.DeliveryStatus) &&
            !request.DeliveryStatus.Equals("all", StringComparison.OrdinalIgnoreCase);

        List<Order> page;
        int totalCount;

        if (hasDeliveryStatusFilter)
        {
            // Delivery status is computed from license keys rather than stored on the order,
            // so it can only be filtered after materializing the matching orders.
            var orders = await query.ToListAsync(cancellationToken);

            var orderIds = orders.Select(o => o.Id).ToList();
            var licenseKeys = await _dbContext.OrderLicenseKeys
                .AsNoTracking()
                .Where(k => orderIds.Contains(k.OrderId))
                .ToListAsync(cancellationToken);

            var keysByOrder = licenseKeys
                .GroupBy(k => k.OrderId)
                .ToDictionary(g => g.Key, g => (IReadOnlyList<OrderLicenseKey>)g.ToList());

            orders = orders
                .Where(o => AdminOrderMapper.ResolveDeliveryStatus(
                    o,
                    keysByOrder.TryGetValue(o.Id, out var keys)
                        ? keys
                        : Array.Empty<OrderLicenseKey>())
                    .Equals(request.DeliveryStatus, StringComparison.OrdinalIgnoreCase))
                .ToList();

            totalCount = orders.Count;
            page = orders
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList();
        }
        else
        {
            totalCount = await query.CountAsync(cancellationToken);
            page = await query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);
        }

        var pageOrderIds = page.Select(o => o.Id).ToList();
        var promotions = await _dbContext.OrderAppliedPromotions
            .AsNoTracking()
            .Where(p => pageOrderIds.Contains(p.OrderId))
            .ToListAsync(cancellationToken);

        var pageLicenseKeys = await _dbContext.OrderLicenseKeys
            .AsNoTracking()
            .Where(k => pageOrderIds.Contains(k.OrderId))
            .ToListAsync(cancellationToken);

        var promotionsByOrder = promotions
            .GroupBy(p => p.OrderId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<OrderAppliedPromotion>)g.ToList());

        var keysByOrderPage = pageLicenseKeys
            .GroupBy(k => k.OrderId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<OrderLicenseKey>)g.ToList());

        var items = page.Select(order =>
        {
            promotionsByOrder.TryGetValue(order.Id, out var orderPromotions);
            keysByOrderPage.TryGetValue(order.Id, out var keys);
            return AdminOrderMapper.ToListItem(
                order,
                orderPromotions ?? Array.Empty<OrderAppliedPromotion>(),
                keys ?? Array.Empty<OrderLicenseKey>());
        }).ToList();

        return Result.Success(new PagedResult<AdminOrderListItemDto>(
            items,
            pageNumber,
            pageSize,
            totalCount));
    }

    private IQueryable<Order> ApplySearch(IQueryable<Order> query, string? searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return query;
        }

        var term = searchTerm.Trim();
        var termHash = OrderLicenseKey.Hash(term);
        var licenseOrderIds = _dbContext.OrderLicenseKeys
            .AsNoTracking()
            .Where(k => k.LicenseKeyHash == termHash)
            .Select(k => k.OrderId);

        return query.Where(o =>
            o.OrderNumber.Contains(term) ||
            o.Email.Contains(term) ||
            o.Items.Any(i =>
                i.ProductNameEn.Contains(term) ||
                (i.VariantSku != null && i.VariantSku.Contains(term))) ||
            licenseOrderIds.Contains(o.Id));
    }

    private static IQueryable<Order> ApplyOrderKindFilter(IQueryable<Order> query, string? orderKind)
    {
        if (string.IsNullOrWhiteSpace(orderKind) || orderKind.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            return query;
        }

        if (!Enum.TryParse<OrderKind>(orderKind, ignoreCase: true, out var kind))
        {
            return query;
        }

        return query.Where(o => o.Kind == kind);
    }

    private static IQueryable<Order> ApplyOrderStatusFilter(IQueryable<Order> query, string? status)
    {
        if (string.IsNullOrWhiteSpace(status) || status.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            return query;
        }

        return status.ToLowerInvariant() switch
        {
            "pending" => query.Where(o => o.Status == OrderStatus.Pending && o.PaymentStatus == PaymentStatus.Pending),
            "processing" => query.Where(o =>
                o.Status == OrderStatus.Processing ||
                (o.Status == OrderStatus.Pending && o.PaymentStatus == PaymentStatus.Paid)),
            "completed" => query.Where(o => o.Status == OrderStatus.Completed),
            "cancelled" => query.Where(o => o.Status == OrderStatus.Cancelled),
            "refunded" => query.Where(o => o.Status == OrderStatus.Refunded || o.PaymentStatus == PaymentStatus.Refunded),
            "failed" => query.Where(o => o.Status == OrderStatus.Failed || o.PaymentStatus == PaymentStatus.Failed),
            _ => query,
        };
    }

    private static IQueryable<Order> ApplyPaymentStatusFilter(IQueryable<Order> query, string? status)
    {
        if (string.IsNullOrWhiteSpace(status) || status.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            return query;
        }

        if (!Enum.TryParse<PaymentStatus>(status, ignoreCase: true, out var paymentStatus))
        {
            return query;
        }

        return query.Where(o => o.PaymentStatus == paymentStatus);
    }

    private static IQueryable<Order> ApplyAmountFilter(IQueryable<Order> query, decimal? minAmount, decimal? maxAmount)
    {
        if (minAmount is decimal min)
        {
            query = query.Where(o => o.TotalAmount >= min);
        }

        if (maxAmount is decimal max)
        {
            query = query.Where(o => o.TotalAmount <= max);
        }

        return query;
    }

    private static IQueryable<Order> ApplyDateFilter(
        IQueryable<Order> query,
        string? dateRange,
        DateTimeOffset? dateFrom,
        DateTimeOffset? dateTo)
    {
        var now = DateTimeOffset.UtcNow;
        DateTimeOffset? from = dateFrom;
        DateTimeOffset? to = dateTo;

        if (!string.IsNullOrWhiteSpace(dateRange) && !dateRange.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            switch (dateRange.ToLowerInvariant())
            {
                case "today":
                    from = new DateTimeOffset(now.UtcDateTime.Date, TimeSpan.Zero);
                    to = from.Value.AddDays(1);
                    break;
                case "yesterday":
                    to = new DateTimeOffset(now.UtcDateTime.Date, TimeSpan.Zero);
                    from = to.Value.AddDays(-1);
                    break;
                case "last7":
                    from = now.AddDays(-7);
                    break;
                case "last30":
                    from = now.AddDays(-30);
                    break;
            }
        }

        if (from is not null)
        {
            query = query.Where(o => o.CreatedOnUtc >= from.Value);
        }

        if (to is not null)
        {
            query = query.Where(o => o.CreatedOnUtc < to.Value);
        }

        return query;
    }

    private static IQueryable<Order> ApplySort(IQueryable<Order> query, string? sort) =>
        sort?.ToLowerInvariant() switch
        {
            "oldest" => query.OrderBy(o => o.CreatedOnUtc),
            "highest" => query.OrderByDescending(o => o.TotalAmount),
            "lowest" => query.OrderBy(o => o.TotalAmount),
            "customer" => query.OrderBy(o => o.Email),
            _ => query.OrderByDescending(o => o.CreatedOnUtc),
        };
}
