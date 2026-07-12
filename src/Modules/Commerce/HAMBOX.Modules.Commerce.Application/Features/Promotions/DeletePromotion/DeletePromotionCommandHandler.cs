using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Errors;
using HAMBOX.Modules.Commerce.Application.Services;
using HAMBOX.Modules.Commerce.Domain.Promotions;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Commerce.Application.Features.Promotions.DeletePromotion;

public sealed record DeletePromotionCommand(Guid Id) : IRequest<Result>;

internal sealed class DeletePromotionCommandHandler : IRequestHandler<DeletePromotionCommand, Result>
{
    private readonly ICommerceDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    public DeletePromotionCommandHandler(ICommerceDbContext dbContext, ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
    }

    public async Task<Result> Handle(DeletePromotionCommand request, CancellationToken cancellationToken)
    {
        var promotion = await _dbContext.Promotions.FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken);
        if (promotion is null)
        {
            return Result.Failure(CommerceErrors.PromotionNotFound);
        }

        PromotionAuditWriter.Write(
            _dbContext,
            promotion.Id,
            null,
            PromotionAuditAction.Deleted,
            _currentUserService.UserId,
            $"Promotion '{promotion.Name}' deleted.");

        _dbContext.Promotions.Remove(promotion);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
