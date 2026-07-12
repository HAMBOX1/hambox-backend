using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Contracts.Promotions;
using HAMBOX.Modules.Commerce.Application.Errors;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Commerce.Application.Features.Promotions.GetPromotionStatistics;

public sealed record GetPromotionStatisticsQuery(Guid Id) : IRequest<Result<PromotionStatisticsDto>>;

internal sealed class GetPromotionStatisticsQueryHandler
    : IRequestHandler<GetPromotionStatisticsQuery, Result<PromotionStatisticsDto>>
{
    private readonly ICommerceDbContext _dbContext;

    public GetPromotionStatisticsQueryHandler(ICommerceDbContext dbContext) => _dbContext = dbContext;

    public async Task<Result<PromotionStatisticsDto>> Handle(
        GetPromotionStatisticsQuery request,
        CancellationToken cancellationToken)
    {
        var promotionExists = await _dbContext.Promotions.AnyAsync(p => p.Id == request.Id, cancellationToken);
        if (!promotionExists)
        {
            return Result.Failure<PromotionStatisticsDto>(CommerceErrors.PromotionNotFound);
        }

        var redemptions = _dbContext.PromotionRedemptions.Where(r => r.PromotionId == request.Id);
        var totalRedemptions = await redemptions.CountAsync(cancellationToken);
        var totalDiscount = await redemptions.SumAsync(r => (decimal?)r.DiscountAmount, cancellationToken) ?? 0m;
        var uniqueUsers = await redemptions
            .Where(r => r.UserId != null)
            .Select(r => r.UserId)
            .Distinct()
            .CountAsync(cancellationToken);

        var now = DateTime.UtcNow;
        var coupons = await _dbContext.CouponCodes
            .Where(c => c.PromotionId == request.Id)
            .ToListAsync(cancellationToken);

        var activeCoupons = coupons.Count(c => c.IsActive && !c.IsExpired(now));
        var expiredCoupons = coupons.Count(c => c.IsExpired(now));

        return Result.Success(new PromotionStatisticsDto(
            request.Id,
            totalRedemptions,
            totalDiscount,
            uniqueUsers,
            activeCoupons,
            expiredCoupons));
    }
}
