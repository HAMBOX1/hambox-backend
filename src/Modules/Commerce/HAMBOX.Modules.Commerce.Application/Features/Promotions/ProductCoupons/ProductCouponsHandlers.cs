using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Errors;
using HAMBOX.Modules.Commerce.Domain.Promotions;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Commerce.Application.Features.Promotions.ProductCoupons;

/// <summary>The body of "show this code on the product pages".</summary>
public sealed record SetPromotionProductDisplayRequest(bool Show);

/// <summary>A promo code shown to customers on a product page ("With coupon code: X").</summary>
public sealed record ProductCouponDto(string Code, string DiscountType, decimal DiscountValue);

/// <summary>Turns the display of a promotion's code on the product pages it applies to on or off.</summary>
public sealed record SetPromotionProductDisplayCommand(Guid PromotionId, bool Show) : IRequest<Result>;

internal sealed class SetPromotionProductDisplayCommandHandler(ICommerceDbContext dbContext)
    : IRequestHandler<SetPromotionProductDisplayCommand, Result>
{
    public async Task<Result> Handle(SetPromotionProductDisplayCommand command, CancellationToken cancellationToken)
    {
        var promotion = await dbContext.Promotions.FirstOrDefaultAsync(p => p.Id == command.PromotionId, cancellationToken);
        if (promotion is null)
        {
            return Result.Failure(CommerceErrors.PromotionNotFound);
        }

        promotion.SetShowOnProductPages(command.Show);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

/// <summary>
/// The codes to show on one product's page: coupon-code promotions the admin chose to show that are running now and apply
/// to this product (a promotion with no targets applies to everything). Codes that are expired, switched off or used up
/// are left out, so the page never advertises a code that would be refused.
/// </summary>
public sealed record GetProductCouponsQuery(Guid ProductId, Guid? CategoryId) : IRequest<Result<IReadOnlyList<ProductCouponDto>>>;

internal sealed class GetProductCouponsQueryHandler(ICommerceDbContext dbContext)
    : IRequestHandler<GetProductCouponsQuery, Result<IReadOnlyList<ProductCouponDto>>>
{
    private const int MaxShown = 3;

    public async Task<Result<IReadOnlyList<ProductCouponDto>>> Handle(
        GetProductCouponsQuery query, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var promotions = await dbContext.Promotions
            .AsNoTracking()
            .Include(p => p.CouponCodes)
            .Include(p => p.Targets)
            .Where(p => p.ShowOnProductPages && p.Type == PromotionType.CouponCode)
            .OrderByDescending(p => p.CreatedOnUtc)
            .ToListAsync(cancellationToken);

        var shown = new List<ProductCouponDto>();
        foreach (var promotion in promotions)
        {
            if (!promotion.IsActiveAt(now) || !AppliesTo(promotion, query))
            {
                continue;
            }

            foreach (var coupon in promotion.CouponCodes)
            {
                if (coupon.IsActive && !coupon.IsExpired(now) && coupon.HasUsesRemaining())
                {
                    shown.Add(new ProductCouponDto(coupon.Code, promotion.DiscountType.ToString(), promotion.DiscountValue));
                    break;
                }
            }

            if (shown.Count >= MaxShown)
            {
                break;
            }
        }

        return Result.Success<IReadOnlyList<ProductCouponDto>>(shown);
    }

    private static bool AppliesTo(Promotion promotion, GetProductCouponsQuery query)
    {
        if (promotion.Targets.Count == 0)
        {
            return true;
        }

        return promotion.Targets.Any(t =>
            (t.TargetType == PromotionTargetType.Product && t.TargetId == query.ProductId)
            || (t.TargetType == PromotionTargetType.Category && query.CategoryId is { } category && t.TargetId == category));
    }
}
