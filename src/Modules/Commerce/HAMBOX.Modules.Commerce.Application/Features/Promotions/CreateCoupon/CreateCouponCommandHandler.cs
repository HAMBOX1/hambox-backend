using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Contracts.Promotions;
using HAMBOX.Modules.Commerce.Application.Errors;
using HAMBOX.Modules.Commerce.Application.Services;
using HAMBOX.Modules.Commerce.Domain.Promotions;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Commerce.Application.Features.Promotions.CreateCoupon;

public sealed record CreateCouponCommand(Guid PromotionId, CreateCouponRequest Request)
    : IRequest<Result<CouponCodeDto>>;

internal sealed class CreateCouponCommandHandler : IRequestHandler<CreateCouponCommand, Result<CouponCodeDto>>
{
    private readonly ICommerceDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    public CreateCouponCommandHandler(ICommerceDbContext dbContext, ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
    }

    public async Task<Result<CouponCodeDto>> Handle(CreateCouponCommand command, CancellationToken cancellationToken)
    {
        var promotion = await _dbContext.Promotions
            .Include(p => p.CouponCodes)
            .FirstOrDefaultAsync(p => p.Id == command.PromotionId, cancellationToken);

        if (promotion is null)
        {
            return Result.Failure<CouponCodeDto>(CommerceErrors.PromotionNotFound);
        }

        if (promotion.Type != PromotionType.CouponCode)
        {
            return Result.Failure<CouponCodeDto>(
                new HAMBOX.SharedKernel.Errors.Error("Coupons.InvalidPromotionType", "Coupons can only be added to coupon promotions."));
        }

        var normalized = CouponCode.NormalizeCode(command.Request.Code);
        var exists = await _dbContext.CouponCodes.AnyAsync(c => c.Code == normalized, cancellationToken);
        if (exists)
        {
            return Result.Failure<CouponCodeDto>(
                new HAMBOX.SharedKernel.Errors.Error("Coupons.DuplicateCode", "A coupon with this code already exists."));
        }

        var coupon = promotion.AddCouponCode(
            command.Request.Code,
            command.Request.IsSingleUse,
            command.Request.MaxUses,
            command.Request.PerUserMaxUses,
            command.Request.ExpiresOnUtc);

        PromotionAuditWriter.Write(
            _dbContext,
            promotion.Id,
            coupon.Id,
            PromotionAuditAction.CouponGenerated,
            _currentUserService.UserId,
            $"Custom coupon {coupon.Code} created.");

        await _dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success(PromotionMapper.ToCouponDto(coupon));
    }
}
