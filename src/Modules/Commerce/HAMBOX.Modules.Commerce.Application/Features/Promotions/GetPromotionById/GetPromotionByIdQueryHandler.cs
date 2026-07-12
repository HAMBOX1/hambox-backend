using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Contracts.Promotions;
using HAMBOX.Modules.Commerce.Application.Errors;
using HAMBOX.Modules.Commerce.Application.Services;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Commerce.Application.Features.Promotions.GetPromotionById;

public sealed record GetPromotionByIdQuery(Guid Id) : IRequest<Result<PromotionDetailDto>>;

internal sealed class GetPromotionByIdQueryHandler : IRequestHandler<GetPromotionByIdQuery, Result<PromotionDetailDto>>
{
    private readonly ICommerceDbContext _dbContext;

    public GetPromotionByIdQueryHandler(ICommerceDbContext dbContext) => _dbContext = dbContext;

    public async Task<Result<PromotionDetailDto>> Handle(GetPromotionByIdQuery request, CancellationToken cancellationToken)
    {
        var promotion = await _dbContext.Promotions
            .Include(p => p.Conditions)
            .Include(p => p.Targets)
            .Include(p => p.CouponCodes)
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken);

        if (promotion is null)
        {
            return Result.Failure<PromotionDetailDto>(CommerceErrors.PromotionNotFound);
        }

        return Result.Success(PromotionMapper.ToDetail(promotion));
    }
}
