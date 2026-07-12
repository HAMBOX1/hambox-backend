using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Contracts.Promotions;
using HAMBOX.Modules.Commerce.Application.Errors;
using HAMBOX.Modules.Commerce.Application.Services;
using HAMBOX.Modules.Commerce.Domain.Promotions;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Commerce.Application.Features.Promotions.SetPromotionStatus;

public enum PromotionStatusAction
{
    Publish,
    Activate,
    Deactivate,
    Archive,
}

public sealed record SetPromotionStatusCommand(Guid Id, PromotionStatusAction Action)
    : IRequest<Result<PromotionDetailDto>>;

internal sealed class SetPromotionStatusCommandHandler : IRequestHandler<SetPromotionStatusCommand, Result<PromotionDetailDto>>
{
    private readonly ICommerceDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    public SetPromotionStatusCommandHandler(ICommerceDbContext dbContext, ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
    }

    public async Task<Result<PromotionDetailDto>> Handle(SetPromotionStatusCommand request, CancellationToken cancellationToken)
    {
        var promotion = await _dbContext.Promotions
            .Include(p => p.Conditions)
            .Include(p => p.Targets)
            .Include(p => p.CouponCodes)
            .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken);

        if (promotion is null)
        {
            return Result.Failure<PromotionDetailDto>(CommerceErrors.PromotionNotFound);
        }

        switch (request.Action)
        {
            case PromotionStatusAction.Publish:
                promotion.Publish();
                PromotionAuditWriter.Write(_dbContext, promotion.Id, null, PromotionAuditAction.Published, _currentUserService.UserId);
                break;
            case PromotionStatusAction.Activate:
                promotion.Activate();
                break;
            case PromotionStatusAction.Deactivate:
                promotion.Unpublish();
                break;
            case PromotionStatusAction.Archive:
                promotion.Archive();
                break;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success(PromotionMapper.ToDetail(promotion));
    }
}
