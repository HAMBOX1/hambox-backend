using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Contracts.Orders;
using HAMBOX.Modules.Commerce.Domain.Enums;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Commerce.Application.Features.Orders.Admin.GetAdminOrderStatistics;

public sealed record GetAdminOrderStatisticsQuery() : IRequest<Result<AdminOrderStatisticsDto>>;

internal sealed class GetAdminOrderStatisticsQueryHandler
    : IRequestHandler<GetAdminOrderStatisticsQuery, Result<AdminOrderStatisticsDto>>
{
    private readonly ICommerceDbContext _dbContext;

    public GetAdminOrderStatisticsQueryHandler(ICommerceDbContext dbContext) => _dbContext = dbContext;

    public async Task<Result<AdminOrderStatisticsDto>> Handle(
        GetAdminOrderStatisticsQuery request,
        CancellationToken cancellationToken)
    {
        var todayStart = new DateTimeOffset(DateTime.UtcNow.Date, TimeSpan.Zero);
        var tomorrowStart = todayStart.AddDays(1);

        var orders = await _dbContext.Orders.AsNoTracking().ToListAsync(cancellationToken);

        var todaysOrders = orders.Count(o => o.CreatedOnUtc >= todayStart && o.CreatedOnUtc < tomorrowStart);
        var pendingOrders = orders.Count(o =>
            o.Status is OrderStatus.Pending or OrderStatus.Processing ||
            (o.Status == OrderStatus.Pending && o.PaymentStatus == PaymentStatus.Paid));

        var completedToday = orders
            .Where(o => o.Status == OrderStatus.Completed && o.CreatedOnUtc >= todayStart && o.CreatedOnUtc < tomorrowStart)
            .ToList();

        var revenueToday = completedToday.Sum(o => o.TotalAmount);
        var averageOrderValue = completedToday.Count > 0
            ? Math.Round(revenueToday / completedToday.Count, 2)
            : 0m;

        var refunds = orders.Count(o =>
            o.Status == OrderStatus.Refunded || o.PaymentStatus == PaymentStatus.Refunded);

        return Result.Success(new AdminOrderStatisticsDto(
            todaysOrders,
            pendingOrders,
            revenueToday,
            averageOrderValue,
            refunds));
    }
}
