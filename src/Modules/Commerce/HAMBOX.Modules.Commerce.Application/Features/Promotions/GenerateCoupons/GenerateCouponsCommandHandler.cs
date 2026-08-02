using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Contracts.Promotions;
using HAMBOX.Modules.Commerce.Application.Errors;
using HAMBOX.Modules.Commerce.Application.Promotions;
using HAMBOX.Modules.Commerce.Application.Services;
using HAMBOX.Modules.Commerce.Domain.Promotions;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Commerce.Application.Features.Promotions.GenerateCoupons;

public sealed record GenerateCouponsCommand(Guid PromotionId, GenerateCouponsRequest Request)
    : IRequest<Result<IReadOnlyList<CouponCodeDto>>>;

internal sealed class GenerateCouponsCommandHandler : IRequestHandler<GenerateCouponsCommand, Result<IReadOnlyList<CouponCodeDto>>>
{
    private readonly ICommerceDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    public GenerateCouponsCommandHandler(ICommerceDbContext dbContext, ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
    }

    public async Task<Result<IReadOnlyList<CouponCodeDto>>> Handle(
        GenerateCouponsCommand command,
        CancellationToken cancellationToken)
    {
        var promotion = await _dbContext.Promotions
            .Include(p => p.CouponCodes)
            .FirstOrDefaultAsync(p => p.Id == command.PromotionId, cancellationToken);

        if (promotion is null)
        {
            return Result.Failure<IReadOnlyList<CouponCodeDto>>(CommerceErrors.PromotionNotFound);
        }

        if (promotion.Type != PromotionType.CouponCode)
        {
            return Result.Failure<IReadOnlyList<CouponCodeDto>>(
                new HAMBOX.SharedKernel.Errors.Error("Coupons.InvalidPromotionType", "Coupons can only be generated for coupon promotions."));
        }

        var request = command.Request;
        var created = new List<CouponCodeDto>();
        var existingCodes = await _dbContext.CouponCodes.Select(c => c.Code).ToListAsync(cancellationToken);
        var existingSet = existingCodes.ToHashSet(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < request.Count; i++)
        {
            string code;
            var attempts = 0;
            do
            {
                code = CouponCodeGenerator.Generate(prefix: request.Prefix);
                attempts++;
            } while (existingSet.Contains(code) && attempts < 20);

            if (existingSet.Contains(code))
            {
                continue;
            }

            var coupon = promotion.AddCouponCode(
                code,
                request.IsSingleUse,
                request.MaxUses,
                request.PerUserMaxUses,
                request.ExpiresOnUtc);

            existingSet.Add(code);
            created.Add(PromotionMapper.ToCouponDto(coupon));
        }

        PromotionAuditWriter.Write(
            _dbContext,
            promotion.Id,
            null,
            PromotionAuditAction.CouponGenerated,
            _currentUserService.UserId,
            $"Generated {created.Count} coupon codes.");

        await _dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success<IReadOnlyList<CouponCodeDto>>(created);
    }
}
