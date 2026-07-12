using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Contracts.Promotions;
using HAMBOX.Modules.Commerce.Application.Errors;
using HAMBOX.Modules.Commerce.Application.Services;
using HAMBOX.Modules.Commerce.Domain.Promotions;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Commerce.Application.Features.Promotions.DuplicatePromotion;

public sealed record DuplicatePromotionCommand(Guid Id, string? NewName) : IRequest<Result<PromotionDetailDto>>;

internal sealed class DuplicatePromotionCommandHandler : IRequestHandler<DuplicatePromotionCommand, Result<PromotionDetailDto>>
{
    private readonly ICommerceDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    public DuplicatePromotionCommandHandler(ICommerceDbContext dbContext, ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
    }

    public async Task<Result<PromotionDetailDto>> Handle(DuplicatePromotionCommand request, CancellationToken cancellationToken)
    {
        var source = await _dbContext.Promotions
            .Include(p => p.Conditions)
            .Include(p => p.Targets)
            .Include(p => p.CouponCodes)
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken);

        if (source is null)
        {
            return Result.Failure<PromotionDetailDto>(CommerceErrors.PromotionNotFound);
        }

        var copy = source.Duplicate(request.NewName ?? $"{source.Name} (Copy)");
        _dbContext.Promotions.Add(copy);

        PromotionAuditWriter.Write(
            _dbContext,
            copy.Id,
            null,
            PromotionAuditAction.Created,
            _currentUserService.UserId,
            $"Duplicated from promotion {source.Id}.");

        await _dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success(PromotionMapper.ToDetail(copy));
    }
}
