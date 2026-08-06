using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Contracts.Promotions;
using HAMBOX.Modules.Commerce.Application.Errors;
using HAMBOX.Modules.Commerce.Application.Services;
using HAMBOX.Modules.Commerce.Domain.Promotions;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Commerce.Application.Features.Promotions.UpdatePromotion;

public sealed record UpdatePromotionCommand(Guid Id, UpdatePromotionRequest Request)
    : IRequest<Result<PromotionDetailDto>>;

internal sealed class UpdatePromotionCommandHandler : IRequestHandler<UpdatePromotionCommand, Result<PromotionDetailDto>>
{
    private readonly ICommerceDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    public UpdatePromotionCommandHandler(ICommerceDbContext dbContext, ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
    }

    public async Task<Result<PromotionDetailDto>> Handle(UpdatePromotionCommand command, CancellationToken cancellationToken)
    {
        var promotion = await _dbContext.Promotions
            .Include(p => p.Conditions)
            .Include(p => p.Targets)
            .Include(p => p.CouponCodes)
            .FirstOrDefaultAsync(p => p.Id == command.Id, cancellationToken);

        if (promotion is null)
        {
            return Result.Failure<PromotionDetailDto>(CommerceErrors.PromotionNotFound);
        }

        var request = command.Request;
        var discountType = PromotionMapper.ParseDiscountType(request.DiscountType);

        promotion.UpdateDetails(
            request.Name,
            request.Description,
            discountType,
            request.DiscountValue,
            request.StartDateUtc,
            request.EndDateUtc);

        // Promotion is tracked as Unchanged (loaded via Include); EF only discovers the new
        // conditions/targets by graph traversal, and since their Guid keys are already set they
        // would otherwise be tracked as Modified (UPDATE against non-existent rows) instead of Added.
        var newConditions = promotion.ReplaceConditions(PromotionMapper.ParseConditions(request.Conditions));
        var newTargets = promotion.ReplaceTargets(PromotionMapper.ParseTargets(request.Targets));
        _dbContext.PromotionConditions.AddRange(newConditions);
        _dbContext.PromotionTargets.AddRange(newTargets);

        PromotionAuditWriter.Write(
            _dbContext,
            promotion.Id,
            null,
            PromotionAuditAction.Updated,
            _currentUserService.UserId,
            $"Promotion '{promotion.Name}' updated.");

        await _dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success(PromotionMapper.ToDetail(promotion));
    }
}
