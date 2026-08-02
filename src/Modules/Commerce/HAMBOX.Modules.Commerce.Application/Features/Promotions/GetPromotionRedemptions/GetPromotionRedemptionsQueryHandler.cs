using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Contracts.Promotions;
using HAMBOX.Modules.Commerce.Application.Errors;
using HAMBOX.SharedKernel.Results;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Commerce.Application.Features.Promotions.GetPromotionRedemptions;

public sealed record GetPromotionRedemptionsQuery(Guid Id, int PageNumber, int PageSize)
    : IRequest<Result<PagedResult<PromotionRedemptionDto>>>;

internal sealed class GetPromotionRedemptionsQueryHandler
    : IRequestHandler<GetPromotionRedemptionsQuery, Result<PagedResult<PromotionRedemptionDto>>>
{
    private readonly ICommerceDbContext _dbContext;

    public GetPromotionRedemptionsQueryHandler(ICommerceDbContext dbContext) => _dbContext = dbContext;

    public async Task<Result<PagedResult<PromotionRedemptionDto>>> Handle(
        GetPromotionRedemptionsQuery request,
        CancellationToken cancellationToken)
    {
        // IgnoreQueryFilters: redemption history for a soft-deleted promotion must remain viewable —
        // it must never become unreachable just because the promotion was deleted.
        var promotionExists = await _dbContext.Promotions
            .IgnoreQueryFilters()
            .AnyAsync(p => p.Id == request.Id, cancellationToken);
        if (!promotionExists)
        {
            return Result.Failure<PagedResult<PromotionRedemptionDto>>(CommerceErrors.PromotionNotFound);
        }

        var query = _dbContext.PromotionRedemptions
            .Where(r => r.PromotionId == request.Id)
            .OrderByDescending(r => r.RedeemedOnUtc);

        var total = await query.CountAsync(cancellationToken);
        var rows = await query
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var couponIds = rows.Where(r => r.CouponCodeId.HasValue).Select(r => r.CouponCodeId!.Value).Distinct().ToList();
        var couponCodes = couponIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await _dbContext.CouponCodes
                .Where(c => couponIds.Contains(c.Id))
                .ToDictionaryAsync(c => c.Id, c => c.Code, cancellationToken);

        var items = rows.Select(r => new PromotionRedemptionDto(
            r.Id,
            r.PromotionName,
            r.CouponCodeId.HasValue && couponCodes.TryGetValue(r.CouponCodeId.Value, out var code) ? code : null,
            r.UserId,
            r.OrderId,
            r.DiscountAmount,
            r.RedeemedOnUtc)).ToList();

        return Result.Success(new PagedResult<PromotionRedemptionDto>(
            items,
            request.PageNumber,
            request.PageSize,
            total));
    }
}
